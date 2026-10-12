// Two language tracks share page slugs. Only Learn is sequential; Easing, Guides and Reference are optional.
// astro.config.mjs builds the sidebar from this list; the route middleware, the language switch, and the section art
// read it too, so a page and its twin always sit at the same place in each path.

export const LANGS = {
	csharp: { label: 'C#', long: 'C#', status: 'Beta' },
	gdscript: { label: 'GDScript', long: 'GDScript', status: 'Beta' },
};

/** Groups in path order. `art` picks the drawing beside each page title; `gdscript` overrides a label there.
 *  An entry with `pages` instead of a `slug` is a nested sidebar group; one with a `link` is a hub page (see HUBS). */
export const PATH = [
	{
		label: 'Learn',
		art: 'hop',
		pages: [
			{ link: '/tutorial/', label: 'Tutorial' },
			{ slug: 'installation', label: 'Installation' },
			{ slug: 'quickstart', label: 'Your First Tween' },
			{ slug: 'definitions', label: 'Reuse a Definition' },
			{ slug: 'syntax-sugar', label: 'Syntax & Sugar' },
			{ slug: 'playback', label: 'Await Completion' },
		],
	},
	{
		label: 'Tweening',
		art: 'spacing',
		pages: [
			{ slug: 'anatomy', label: 'Anatomy' },
			{ link: '/easings/', label: 'Easings' },
			{ slug: 'effects', label: 'Effects' },
			{ slug: 'functions', label: 'Functions' },
			{ slug: 'curves', label: 'Godot Curves' },
			{ link: '/porting/', label: 'Porting from Godot' },
		],
	},
	{
		label: 'Guides',
		art: 'sketch',
		collapsed: true,
		pages: [
			{ slug: 'sequences', label: 'Sequences' },
			{ slug: 'layout-transitions', label: 'Layout transitions' },
			{ slug: 'keyframes', label: 'Keyframes' },
			{ slug: 'loops', label: 'Loops & delays' },
			{ slug: 'variations', label: 'Variations' },
			{ slug: 'cancellation', label: 'Cancellation' },
			{ slug: 'lifetime', label: 'Lifetime & pausing' },
			{ slug: 'materials', label: 'Materials & shaders' },
			{ slug: 'custom-properties', label: 'Custom properties' },
		],
	},
	{
		label: 'Reference',
		art: 'catalog',
		collapsed: true,
		pages: [
			{ label: 'Catalog', pages: [
				{ slug: 'nodes', label: 'Overview' },
				{ slug: 'nodes/2d', label: '2D nodes' },
				{ slug: 'nodes/3d', label: '3D nodes' },
				{ slug: 'nodes/ui', label: 'UI controls' },
				{ slug: 'nodes/materials', label: 'Material properties' },
				{ slug: 'nodes/audio', label: 'Animation & audio' },
				{ slug: 'nodes/values', label: 'Callback values' },
			] },
			// Core API pages follow a tween's life: describe it, shape it, start it, control it, then extend the library.
			{ label: 'Core API', pages: [
				{ slug: 'api', label: 'Overview' },
				{ slug: 'api/definitions', label: 'Definitions' },
				{ slug: 'api/endpoints', label: 'Endpoints & variations' },
				{ slug: 'api/timing', label: 'Timing' },
				{ slug: 'api/callbacks', label: 'Callbacks' },
				{ slug: 'api/easing', label: 'Easing' },
				{ slug: 'api/effects', label: 'Effects' },
				{ slug: 'api/start', label: 'Starting playback' },
				{ slug: 'api/handles', label: 'Handles' },
				{ slug: 'api/groups', label: 'Groups' },
				{ slug: 'api/chains', label: 'Chains' },
				{ slug: 'api/custom', label: 'Custom definitions', gdscript: 'Adapters' },
				{ slug: 'api/scheduler', label: 'Scheduler' },
			] },
		],
	},
];

/** Only these pages form the sequential tutorial, also shown on the tutorial hub. */
export const LEARN = PATH[0].pages.filter((page) => page.slug);

/** Pages that belong to neither language, listed under Project. The overview is reached from the site title. */
export const SHARED = [
	{ slug: 'faq', label: 'FAQ' },
	{ slug: 'compatibility', label: 'Compatibility' },
	{ slug: 'advanced-development', label: 'Advanced development' },
	{ slug: 'acknowledgements', label: 'Acknowledgements' },
];
export const SHARED_LABEL = 'Project';

/** 'csharp', 'gdscript', or undefined for a shared page, from a content id or URL path. */
export function trackOf(idOrPath) {
	const first = idOrPath.replace(/^\/+/, '').split('/')[0];
	return first in LANGS ? first : undefined;
}

/** The page slug within its track, e.g. 'definitions' for 'gdscript/definitions'. */
export const slugOf = (idOrPath) => idOrPath.replace(/^\/+|\/+$/g, '').split('/').slice(1).join('/');

/** Every page entry of a group, including those in nested groups. */
export const pagesOf = (group) => group.pages.flatMap((page) => (page.pages ? pagesOf(page) : [page]));

export const firstSlug = LEARN[0].slug;

/** Hub pages are shared pages that both tracks list, such as the tutorial overview and the easing playground. */
export const HUBS = PATH.flatMap(pagesOf).filter((page) => page.link);

/** The hub entry for a content id such as 'tutorial', if the page is one or sits under one, as 'tutorial/in-engine'
 *  does. A page under a hub shares its sidebar and title art. */
export const hubOf = (id) => HUBS.find((page) => `/${id}/`.startsWith(page.link));

// The sidebar numbers the tutorial's steps; the tutorial hub draws its own numbers beside the plain labels.
const label = (page, lang) => {
	const step = LEARN.indexOf(page);
	return step < 0 ? (page[lang] ?? page.label) : `${step + 1}. ${page[lang] ?? page.label}`;
};

const items = (pages, lang) =>
	pages.map((page) =>
		page.pages
			? { label: label(page, lang), collapsed: true, items: items(page.pages, lang) }
			: page.link
				? { label: label(page, lang), link: page.link }
				: { label: label(page, lang), slug: `${lang}/${page.slug}` },
	);

/** Starlight sidebar config: one top-level group per language, then the shared pages. */
export const sidebar = () => [
	...Object.entries(LANGS).map(([lang, { label, status }]) => ({
		label,
		...(status && { badge: { text: status, variant: 'note' } }),
		items: PATH.map((group) => ({ label: group.label, collapsed: group.collapsed ?? false, items: items(group.pages, lang) })),
	})),
	{ label: SHARED_LABEL, collapsed: true, items: SHARED.map(({ slug, label }) => ({ label, slug })) },
];

/** Old single-path URLs, kept working after the split. */
export const redirects = {
	'/csharp/shaders': '/csharp/materials/',
	'/gdscript/shaders': '/gdscript/materials/',
	'/csharp/timing': '/csharp/loops/',
	'/gdscript/timing': '/gdscript/loops/',
	'/csharp/custom-tweens': '/csharp/custom-properties/',
	'/gdscript/custom-tweens': '/gdscript/custom-properties/',
	// Custom easing split into ease functions and Godot curves.
	'/csharp/custom': '/csharp/functions/',
	'/gdscript/custom': '/gdscript/functions/',
	'/concepts/definitions': '/csharp/definitions/',
	'/concepts/easing': '/easings/',
	'/concepts/timing': '/csharp/loops/',
	'/concepts/lifetime': '/csharp/lifetime/',
	// Playback options moved to Starting playback, callbacks to their own page, and each enum to the page it belongs to.
	'/csharp/api/modes': '/csharp/api/start/',
	'/gdscript/api/modes': '/gdscript/api/start/',
	'/csharp/api/enums': '/csharp/api/',
	'/gdscript/api/enums': '/gdscript/api/',
	'/csharp': `/csharp/${firstSlug}/`,
	'/gdscript': `/gdscript/${firstSlug}/`,
};
