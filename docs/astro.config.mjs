// @ts-check
import { readdir, readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import { codeThemeSelector, csharpDark, csharpLight, gdscriptDark, gdscriptLight } from './src/styles/code-themes.mjs';
import { redirects, sidebar } from './src/tracks.mjs';
import { checkSearchUpstream } from './scripts/check-search-upstream.mjs';
import llmsDocumentation from './scripts/generate-llms.mjs';

const elements = (node, tagName) => (node?.children ?? []).filter((c) => c.type === 'element' && c.tagName === tagName);
const firstCode = (node) =>
	node.type === 'element' && node.tagName === 'code' ? node : (node.children ?? []).map(firstCode).find(Boolean);
const anchor = (text) => text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');

// API reference tables list one member per row, named in their first column. They render as a member list (theme.css),
// and each row gets an id from its first code name, so a member can be linked directly.
const MEMBER_COLUMNS = new Set(['Member', 'Field', 'Method', 'Entry point', 'Constructor', 'Factory', 'Enum', 'Constant', 'Parameter']);

// Wraps Markdown tables in a scroll container, so a wide table scrolls inside the content lane instead of spilling past it.
// Each body cell also gets its column header as data-label, which narrow screens show when they stack rows.
// Registered on the Sätteri processor's hast pipeline, the same way Starlight adds its own transforms.
const tableScroll = {
	name: 'tweens-table-scroll',
	hooks: {
		'astro:config:setup': ({ config }) =>
			config.markdown.processor.options.hastPlugins.push({
				name: 'tweens-table-wrap',
				element: [
					{
						filter: ['table'],
						visit: (node, ctx) => {
							const headers = elements(elements(elements(node, 'thead')[0], 'tr')[0], 'th').map((th) =>
								ctx.textContent(th).trim(),
							);
							for (const th of elements(elements(elements(node, 'thead')[0], 'tr')[0], 'th'))
								ctx.setProperty(th, 'scope', 'col');
							const members = MEMBER_COLUMNS.has(headers[0]);
							for (const tr of elements(elements(node, 'tbody')[0], 'tr')) {
								const cells = elements(tr, 'td');
								cells.forEach((td, i) => headers[i] && ctx.setProperty(td, 'dataLabel', headers[i]));
								const name = members && cells[0] && firstCode(cells[0]);
								if (!name) continue;
								// The name links to its own row, so a reader can copy a link to one member.
								const id = anchor(ctx.textContent(name));
								ctx.setProperty(tr, 'id', id);
								ctx.wrapNode(name, { type: 'element', tagName: 'a', properties: { href: `#${id}` }, children: [] });
							}
							const className = members ? ['table-wrap', 'members'] : ['table-wrap'];
							ctx.wrapNode(node, { type: 'element', tagName: 'div', properties: { className }, children: [] });
						},
					},
				],
			}),
	},
};

// Astro doesn't preload the modules a page's scripts import, so a first visit found them only once the script had
// arrived, and every script after it waited that extra round trip. This links each static import up front;
// dynamic import() stays lazy.
const preloadImports = {
	name: 'tweens-modulepreload',
	hooks: {
		'astro:build:done': async ({ dir }) => {
			const root = fileURLToPath(dir);
			const cache = new Map();
			/** @param {string} href @returns {Promise<string[]>} */
			const importsOf = async (href) => {
				if (!cache.has(href)) {
					const code = await readFile(join(root, href), 'utf8');
					const specs = [...code.matchAll(/\bimport\s*(?:[\w$*{}\s,]+from\s*)?["'](\.\.?\/[^"']+)["']/g)];
					cache.set(href, specs.map((m) => new URL(m[1], `https://x${href}`).pathname));
				}
				return cache.get(href);
			};
			const pages = (await readdir(root, { recursive: true })).filter((f) => f.endsWith('.html'));
			for (const page of pages) {
				const file = join(root, page);
				const html = await readFile(file, 'utf8');
				const scripts = [...html.matchAll(/<script type="module" src="([^"]+)"/g)].map((m) => m[1]);
				const found = new Set();
				/** @param {string} href */
				const visit = async (href) => {
					for (const dep of await importsOf(href)) {
						if (found.has(dep)) continue;
						found.add(dep);
						await visit(dep);
					}
				};
				for (const src of scripts) await visit(src);
				for (const src of scripts) found.delete(src);
				const links = [...found].map((href) => `<link rel="modulepreload" href="${href}">`).join('');
				if (links) await writeFile(file, html.replace('</head>', `${links}</head>`));
			}
		},
	},
};

// https://astro.build/config
export default defineConfig({
	site: 'https://tweens.gd',
	redirects,
	// Head.astro opts links into Astro's hover prefetch only when Speculation Rules aren't supported.
	// public/_headers gives HTML a short freshness window so Firefox can reuse the prefetched response.
	prefetch: { prefetchAll: false, defaultStrategy: 'hover' },
	vite: {
		build: {
			// A demo's own small stylesheet goes inline, so a first visit doesn't wait on a render-blocking request
			// for it. Other assets keep Vite's default.
			assetsInlineLimit: (file, content) => (file.endsWith('.css') ? content.length < 10 * 1024 : undefined),
		},
	},
	integrations: [
		{
			name: 'tweens-search-upstream-check',
			hooks: { 'astro:build:start': checkSearchUpstream },
		},
		tableScroll,
		preloadImports,
		llmsDocumentation(),
		starlight({
			title: 'tweens.gd',
			// The custom 404.md already generates /404 through Starlight's content route.
			// Astro emits it as 404.html; a separate default route would duplicate it.
			disable404Route: true,
			description: "A tweening library for Godot (that doesn't suck.)",
			// Starlight emits og:title/description/url and twitter:card; this adds the preview image.
			head: [

			    { tag: 'link', attrs: { rel: "me", href: "https://mastodon.gamedev.place/@jupiter" }},
			    { tag: 'link', attrs: { rel: "me", href: "https://tiggi.es/@thygrrr" }},
				{ tag: 'meta', attrs: { property: 'og:image', content: 'https://tweens.gd/og-card.png' } },
				{ tag: 'meta', attrs: { property: 'og:image:type', content: 'image/png' } },
				{ tag: 'meta', attrs: { property: 'og:image:width', content: '1200' } },
				{ tag: 'meta', attrs: { property: 'og:image:height', content: '630' } },
				{ tag: 'meta', attrs: { property: 'og:image:alt', content: 'The tweens.gd logo: a happy ferret curled around the name' } },
				{ tag: 'meta', attrs: { name: 'twitter:image', content: 'https://tweens.gd/og-card-twitter.png' } },
				{ tag: 'meta', attrs: { name: 'twitter:image:alt', content: 'The tweens.gd logo: a happy ferret curled around the name' } },
				{ tag: 'meta', attrs: { name: 'theme-color', content: '#0e1620' } },
			],
			tableOfContents: false,
			customCss: [
				'@fontsource-variable/figtree',
				'@fontsource-variable/jetbrains-mono',
				'./src/styles/theme.css',
			],
			components: {
				Head: './src/components/overrides/Head.astro',
				Hero: './src/components/overrides/Hero.astro',
				PageTitle: './src/components/overrides/PageTitle.astro',
				SiteTitle: './src/components/overrides/SiteTitle.astro',
				MarkdownContent: './src/components/overrides/MarkdownContent.astro',
				Header: './src/components/overrides/Header.astro',
				Footer: './src/components/overrides/Footer.astro',
				Sidebar: './src/components/overrides/Sidebar.astro',
				ThemeProvider: './src/components/overrides/ThemeProvider.astro',
				ThemeSelect: './src/components/overrides/ThemeSelect.astro',
			},
			expressiveCode: {
				themes: [csharpDark, csharpLight, gdscriptDark, gdscriptLight],
				themeCssSelector: codeThemeSelector,
				styleOverrides: {
					borderRadius: '0.9rem',
					borderColor: 'var(--tw-outline)',
					codeFontFamily: 'var(--__sl-font-mono)',
					uiFontFamily: 'var(--__sl-font)',
					codeBackground: ({ theme }) => (theme.name.startsWith('gdscript') ? 'var(--tw-code-bg-gd)' : 'var(--tw-code-bg-cs)'),
					frames: {
						editorTabBarBackground: 'var(--tw-surface)',
						// The theme's accent stripe on the active tab gets clipped by the frame's corner radius.
						editorActiveTabIndicatorTopColor: 'transparent',
						editorActiveTabIndicatorBottomColor: 'transparent',
						terminalTitlebarBackground: 'var(--tw-surface)',
						frameBoxShadowCssValue: 'var(--tw-shadow)',
					},
				},
			},
			social: [
				{ icon: 'seti:godot', label: 'Godot Asset Store', href: 'https://store.godotengine.org/asset/outfox/tweens/' },
				{ icon: 'discord', label: 'Discord', href: 'https://discord.gg/3UXVHnmEwd' },
				{ icon: 'github', label: 'GitHub', href: 'https://github.com/outfox/tweens.gd' },
			],
			// One learning path per language (src/tracks.mjs); route middleware shows only the current page's path.
			sidebar: sidebar(),
			routeMiddleware: './src/routeData.ts',
		}),
	],
});
