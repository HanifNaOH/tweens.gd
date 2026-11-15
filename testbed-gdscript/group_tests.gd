# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
extends RefCounted

const T = preload("res://addons/tweens_gd/tweens.gd")
const Suite = preload("res://tests.gd")

var _host: Suite

func run(host: Node) -> bool:
	_host = host
	for test: Callable in [_conformance, _completion, _controls, _lifetimes, _failures, _reentrancy, _mixed_clocks, _independent_roots, _references]:
		var returned: bool = test.call() == true
		check(returned, "group test returned normally: " + test.get_method())
	await _waits_and_validation()
	return true

func check(condition: bool, message: String) -> void:
	_host.check(condition, message)

func near(actual: float, expected: float, message: String) -> void:
	_host.near(actual, expected, message)

func start(scheduler: TweensGdScheduler, duration: float = 1.0) -> TweensGdHandle:
	return scheduler.add(RefCounted.new(), T.value(0.0, 1.0, duration))

func _completion() -> bool:
	var scheduler := TweensGdScheduler.new()
	var a := start(scheduler)
	var b := start(scheduler, 2.0)
	var input := [a, b, a]
	var group := T.group(input)
	input.clear()
	var members := group.members
	members.clear()
	check(group.members == [a, b], "group snapshots members and deduplicates handles")
	check(not group.is_terminal and not group.is_settled and group.completion_reason == -1, "new group is pending")
	var results: Array[int] = []
	check(group.ended.connect(func(reason: int) -> void: results.append(reason)) == OK, "group ended signal connects")
	scheduler.update(1.0)
	check(a.is_settled and not group.is_terminal, "group waits for its longest member")
	scheduler.update(1.0)
	check(group.is_terminal and group.is_settled and group.completion_reason == T.Reason.COMPLETED, "group completes after every member")
	check(group.error.is_empty() and group.errors.is_empty(), "successful group has no errors")
	group.pause()
	group.resume()
	group.cancel()
	check(results == [T.Reason.COMPLETED] and not group.is_paused, "settled group controls are safe and completion is one-shot")
	var already := TweensGdGroup.of([a, b])
	check(already.is_settled and already.completion_reason == T.Reason.COMPLETED, "already-completed group settles at creation")
	var c := start(scheduler)
	var partial := T.group([b, c])
	check(not partial.is_settled, "already-settled success does not end a partial group")
	scheduler.update(1.0)
	check(partial.is_settled, "partial group finishes normally")
	scheduler.dispose()
	return true

func _controls() -> bool:
	var scheduler := TweensGdScheduler.new()
	var a := start(scheduler)
	var b := start(scheduler, 2.0)
	var group := T.group([a, b])
	a.pause()
	check(not group.is_paused, "group needs every active member paused")
	group.pause()
	check(group.is_paused and a.is_paused and b.is_paused, "group pauses every active member")
	scheduler.update(2.0)
	check(a.value == null, "group pause defers activation")
	group.is_paused = false
	check(not a.is_paused and not b.is_paused, "group pause property resumes members")
	scheduler.update(1.0)
	b.pause()
	check(group.is_paused, "completed members do not affect group pause status")
	group.resume()
	check(not b.is_paused, "group resume fans out")
	group.cancel()
	check(a.completion_reason == T.Reason.COMPLETED and b.completion_reason == T.Reason.CANCELLED, "group cancel preserves completed members")
	check(group.is_settled and group.completion_reason == T.Reason.CANCELLED and not group.is_paused, "group cancel settles and clears pause status")
	var c := start(scheduler)
	var d := start(scheduler)
	var e := start(scheduler)
	var first := T.group([c, d])
	var second := T.group([d, e])
	c.cancel()
	check(first.is_settled and second.is_settled and e.completion_reason == T.Reason.CANCELLED, "overlapping groups propagate cancellation without recursion")
	scheduler.dispose()
	return true

func _lifetimes() -> bool:
	for reason: int in [T.Reason.TARGET_FREED, T.Reason.OWNER_EXITED, T.Reason.RUNNER_DISPOSED]:
		var scheduler := TweensGdScheduler.new()
		var other := TweensGdScheduler.new()
		var owner := Node.new()
		_host.add_child(owner)
		var target := Object.new()
		var bound := scheduler.add(target, T.value(0.0, 1.0, 1.0), owner)
		var sibling := start(other)
		var group := T.group([bound, sibling])
		group.pause()
		match reason:
			T.Reason.TARGET_FREED:
				target.free()
				scheduler.update(0.0)
			T.Reason.OWNER_EXITED: owner.free()
			T.Reason.RUNNER_DISPOSED: scheduler.dispose()
		check(group.is_settled and group.completion_reason == reason, "group retains first lifetime/disposal reason %d" % reason)
		check(sibling.is_settled and sibling.completion_reason == T.Reason.CANCELLED, "lifetime stop cancels siblings on another scheduler")
		if is_instance_valid(target): target.free()
		if is_instance_valid(owner): owner.free()
		scheduler.dispose()
		other.dispose()
	return true

func _failures() -> bool:
	var scheduler := TweensGdScheduler.new()
	var faulty := T.value(0.0, 1.0, 1.0)
	faulty.ease_function = func(_t: float) -> float: return NAN
	var stale_owner := Node.new()
	var cancellation := T.value(0.0, 1.0, 1.0)
	cancellation.on_cancel = stale_owner.set_process.bind(true).unbind(1)
	var b := scheduler.add(RefCounted.new(), cancellation)
	scheduler.update(0.0)
	var a := scheduler.add(RefCounted.new(), faulty)
	var group := T.group([a, b])
	stale_owner.free()
	scheduler.update(0.5)
	check(group.is_settled and group.completion_reason == T.Reason.FAILED, "detected failure stops group")
	check(b.completion_reason == T.Reason.FAILED, "fault in sibling cancellation is recorded")
	check(group.errors == [a.error, b.error] and group.error == a.error + "\n" + b.error, "group aggregates member errors")
	var errors := group.errors
	errors.clear()
	check(group.errors.size() == 2, "error inspection cannot mutate group")
	for reversed: bool in [false, true]:
		var rejected := scheduler.add(RefCounted.new(), T.value(0.0, 1.0, -1.0))
		var live := start(scheduler)
		var partial := T.group([live, rejected, rejected] if reversed else [rejected, live, rejected])
		check(partial.is_settled and partial.completion_reason == T.Reason.FAILED, "already-rejected member settles group regardless of order")
		check(live.is_settled and live.completion_reason == T.Reason.CANCELLED, "already-rejected member cancels live siblings")
		check(partial.errors == [rejected.error], "duplicate rejected handle contributes one error")
	var done := start(scheduler, 0.0)
	scheduler.update(0.0)
	var cancelled := start(scheduler)
	cancelled.cancel()
	var active := start(scheduler)
	var mixed := T.group([done, cancelled, active])
	check(mixed.is_settled and mixed.completion_reason == T.Reason.CANCELLED, "pre-settled cancellation plus success settles once")
	scheduler.dispose()
	return true

func _reentrancy() -> bool:
	var scheduler := TweensGdScheduler.new()
	var events: Array[String] = []
	var holder: Array[TweensGdGroup] = []
	var definition := T.value(0.0, 1.0, 1.0)
	definition.on_update = func(_h: TweensGdHandle, _v: Variant) -> void:
		events.append("update")
		holder[0].cancel()
		check(not holder[0].is_settled, "group waits for cancelled member's active callback")
		events.append("returned")
	definition.on_finally = func(_h: TweensGdHandle) -> void: events.append("finally")
	var a := scheduler.add(RefCounted.new(), definition)
	var b := start(scheduler)
	var group := T.group([a, b])
	holder.append(group)
	var on_ended := func(_r: int) -> void:
		events.append("group")
		check(a.is_settled and b.is_settled, "group signal sees all members settled")
		group.cancel()
	check(group.ended.connect(on_ended) == OK, "reentrant group ended signal connects")
	scheduler.update(0.5)
	check(events == ["update", "finally", "returned", "group"], "group settlement follows reentrant callback return")
	holder.clear()
	var done := start(scheduler, 0.0)
	scheduler.update(0.0)
	var end := T.value(0.0, 1.0, 1.0)
	end.on_end = func(h: TweensGdHandle) -> void:
		holder.append(T.group([h, done]))
		check(h.is_terminal and not h.is_settled and not holder[0].is_settled, "group built in on_end waits for settlement")
	end.on_finally = func(_h: TweensGdHandle) -> void: check(not holder[0].is_settled, "group remains pending through finally")
	@warning_ignore("return_value_discarded")
	scheduler.add(RefCounted.new(), end)
	scheduler.update(1.0)
	check(holder[0].is_settled and holder[0].completion_reason == T.Reason.COMPLETED, "group created during terminal callback completes")
	holder.clear()
	scheduler.dispose()
	return true

func _continue(group: TweensGdGroup, scheduler: TweensGdScheduler, results: Array, options: TweensGdPlaybackOptions = null) -> void:
	var reason: int = await group.wait()
	results.append(reason)
	results.append(scheduler.add(RefCounted.new(), T.value(0.0, 1.0, 1.0), null, options))

func _mixed_clocks() -> bool:
	for mismatch: String in ["lane", "time", "scheduler", "next_lane", "next_time", "next_scheduler"]:
		var scheduler := TweensGdScheduler.new()
		var other := TweensGdScheduler.new()
		var member_options := T.playback_options()
		if mismatch == "lane": member_options.process_mode = T.Process.PHYSICS
		if mismatch == "time": member_options.use_unscaled_time = true
		var a := start(scheduler)
		var member_scheduler: TweensGdScheduler = other if mismatch == "scheduler" else scheduler
		var b := member_scheduler.add(RefCounted.new(), T.value(0.0, 1.0, 1.0), null, member_options)
		var group := T.group([a, b])
		var next: Array = []
		var next_options := T.playback_options()
		if mismatch in ["lane", "next_lane"]: next_options.process_mode = T.Process.PHYSICS
		if mismatch in ["time", "next_time"]: next_options.use_unscaled_time = true
		var next_scheduler: TweensGdScheduler = other if mismatch in ["scheduler", "next_scheduler"] else scheduler
		@warning_ignore("missing_await")
		_continue(group, next_scheduler, next, next_options)
		match mismatch:
			"lane":
				scheduler.update(1.5, 2.0)
				check(a.is_settled and b.value == null and not group.is_settled and next.is_empty(), "mixed group waits for physics member")
				scheduler.update(1.5, 2.0, T.Process.PHYSICS)
			"time":
				scheduler.update(0.5, 1.5)
				check(b.is_settled and not a.is_terminal and not group.is_settled and next.is_empty(), "mixed group waits for scaled member")
				scheduler.update(1.0, 0.0)
			"scheduler":
				scheduler.update(1.5, 2.0)
				check(a.is_settled and b.value == null and not group.is_settled and next.is_empty(), "mixed group waits for other scheduler")
				other.update(1.5, 2.0)
			_: scheduler.update(1.5, 2.0)
		check(group.is_settled and group.completion_reason == T.Reason.COMPLETED and next.size() == 2, "mixed-clock group resumes: " + mismatch)
		var reason: int = next[0]
		var continued: TweensGdHandle = next[1]
		check(reason == T.Reason.COMPLETED and continued.value == null and continued.progress == 0.0, "continuation inherits no group credit: " + mismatch)
		var wrong_lane: int = T.Process.PROCESS if next_options.process_mode == T.Process.PHYSICS else T.Process.PHYSICS
		next_scheduler.update(0.4, 0.8, wrong_lane)
		check(continued.value == null and continued.progress == 0.0, "continuation waits for eligible lane: " + mismatch)
		var wrong_scheduler: TweensGdScheduler = scheduler if next_scheduler == other else other
		wrong_scheduler.update(0.4, 0.8, next_options.process_mode)
		check(continued.value == null and continued.progress == 0.0, "continuation waits for its scheduler: " + mismatch)
		next_scheduler.update(0.0, 0.0, next_options.process_mode)
		var started_value: float = continued.value
		near(started_value, 0.0, "first eligible update starts without inherited time: " + mismatch)
		near(continued.progress, 0.0, "zero delta gives no continuation credit: " + mismatch)
		next_scheduler.update(0.25, 0.5, next_options.process_mode)
		var advanced_value: float = continued.value
		near(advanced_value, 0.5 if next_options.use_unscaled_time else 0.25, "continuation uses only its own update clock: " + mismatch)
		scheduler.dispose()
		other.dispose()
	return true

func _independent_roots() -> bool:
	var scheduler := TweensGdScheduler.new()
	var other := TweensGdScheduler.new()
	var group := T.group([start(other)])
	var next: Array = []
	@warning_ignore("missing_await")
	_continue(group, scheduler, next)
	var after: Array = []
	var definition := T.value(0.0, 1.0, 0.5)
	definition.on_end = func(_h: TweensGdHandle) -> void:
		group.cancel()
		after.append(start(scheduler))
	@warning_ignore("return_value_discarded")
	scheduler.add(RefCounted.new(), definition)
	scheduler.update(0.75)
	check(next[1].value == null and after[0].value == null, "callback roots defer capture")
	scheduler.update(0.0)
	var continued_value: float = next[1].value
	near(continued_value, 0.0, "group continuation starts fresh")
	var root_value: float = after[0].value
	near(root_value, 0.0, "callback root starts fresh")
	scheduler.dispose()
	other.dispose()
	return true

func _conformance() -> bool:
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://conformance/groups.json"))
	for test: Dictionary in data.cases:
		var case_name: String = test.name
		var scheduler := TweensGdScheduler.new()
		var members: Array[TweensGdHandle] = []
		for duration: float in test.durations: members.append(start(scheduler, duration))
		var ordered: Array[TweensGdHandle] = []
		for index: int in test.order: ordered.append(members[index])
		var group := T.group(ordered)
		var next: Array = []
		@warning_ignore("missing_await")
		_continue(group, scheduler, next)
		for sample: Dictionary in test.samples:
			var delta: float = sample.delta
			scheduler.update(delta)
			var settled: bool = sample.settled
			check(group.is_settled == settled, case_name + " settlement")
			check((next.size() == 2) == settled, case_name + " continuation")
		var reason: int = next[0]
		check(reason == T.Reason.COMPLETED, case_name + " reason")
		var continued: TweensGdHandle = next[1]
		check(continued.value == null, case_name + " continuation waits for next update")
		var next_delta: float = test.next_delta
		scheduler.update(next_delta)
		var root_value: float = continued.value
		var expected: float = test.next_value
		var tolerance: float = data.tolerance
		check(absf(root_value - expected) <= tolerance, case_name + " independent root delta")
		scheduler.dispose()
	for order: Array in data.settled_failure_orders:
		var scheduler := TweensGdScheduler.new()
		var cancelled := start(scheduler)
		cancelled.cancel()
		var running := start(scheduler)
		var failed := scheduler.add(RefCounted.new(), T.value(0.0, 1.0, -1.0))
		var members: Array[TweensGdHandle] = [cancelled, running, failed]
		var ordered: Array[TweensGdHandle] = []
		for index: int in order: ordered.append(members[index])
		var group := T.group(ordered)
		check(group.is_settled, "pre-settled failure orders settle")
		check(running.completion_reason == T.Reason.CANCELLED, "pre-settled failure orders cancel the running member")
		check(group.errors == [failed.error], "pre-settled failure orders collect the failure exactly once")
		scheduler.dispose()
	return true

func _references() -> bool:
	var scheduler := TweensGdScheduler.new()
	var a := start(scheduler)
	var b := start(scheduler)
	var group := T.group([a, b])
	var watcher: WeakRef = weakref(group._watcher)
	var weak_group: WeakRef = weakref(group)
	group = null
	check(weak_group.get_ref() == null, "pending members do not retain group wrapper")
	a.cancel()
	check(b.is_settled, "discarded group still cancels siblings")
	check(watcher.get_ref() == null, "settlement releases discarded group's coordinator")
	var held := T.group([start(scheduler, 0.0)])
	_capture_group(held)
	var weak_held: WeakRef = weakref(held)
	var weak_handle: WeakRef = weakref(held.members[0])
	scheduler.update(0.0)
	held = null
	check(weak_held.get_ref() == null and weak_handle.get_ref() == null, "group releases observer closures and completed members")
	var retained := T.group([start(scheduler, 0.0)])
	scheduler.update(0.0)
	var weak_scheduler: WeakRef = weakref(scheduler)
	scheduler = null
	check(weak_scheduler.get_ref() == null and retained.is_settled, "completed group carries only a weak scheduler reference")
	# Even accidentally dropping an undisposed manual scheduler must not create an ownership cycle.
	var abandoned := TweensGdScheduler.new()
	var dropped := T.group([start(abandoned)])
	var weak_dropped: WeakRef = weakref(dropped)
	var weak_member: WeakRef = weakref(dropped.members[0])
	var weak_watcher: WeakRef = weakref(dropped._watcher)
	dropped = null
	abandoned = null
	check(weak_dropped.get_ref() == null and weak_member.get_ref() == null and weak_watcher.get_ref() == null, "discarding scheduler/group creates no RefCounted cycle")
	return true

func _capture_group(group: TweensGdGroup) -> void:
	check(group.ended.connect(func(_reason: int) -> void: group.cancel()) == OK, "captured group ended signal connects")

func _record_wait(group: TweensGdGroup, results: Array) -> void:
	results.append(await group.wait())

func _temporary_wait(scheduler: TweensGdScheduler, results: Array) -> void:
	results.append(await T.group([start(scheduler)]).wait())

func _waits_and_validation() -> void:
	var scheduler := TweensGdScheduler.new()
	var group := T.group([start(scheduler)])
	var results: Array = []
	@warning_ignore("missing_await")
	_record_wait(group, results)
	@warning_ignore("missing_await")
	_record_wait(group, results)
	@warning_ignore("missing_await")
	_temporary_wait(scheduler, results)
	check(results.is_empty(), "group waiters suspend")
	scheduler.update(1.0)
	check(results == [T.Reason.COMPLETED, T.Reason.COMPLETED, T.Reason.COMPLETED], "multiple and temporary group awaits resume")
	var late: int = await group.wait()
	check(late == T.Reason.COMPLETED, "late group wait returns immediately")
	var repeated: int = await group.wait()
	check(repeated == T.Reason.COMPLETED, "repeated group wait returns cached reason")
	var survivor := start(scheduler)
	for input: Variant in [null, [], 42, [null], [survivor, null], [survivor, 42], [survivor, TweensGdDefinition.new()]]:
		_host.failures.append_array(_host._collector.take_errors())
		var invalid := T.group(input)
		var diagnostics: Array[String] = _host._collector.take_errors()
		check(invalid != null and invalid.is_settled and invalid.is_terminal, "invalid group is non-null and settled")
		var failed: int = await invalid.wait()
		check(failed == T.Reason.FAILED and not invalid.error.is_empty(), "invalid group is immediately awaitable with diagnostic")
		check(diagnostics.size() == 1 and diagnostics[0].contains(invalid.error), "invalid group logs exactly its error")
		check(invalid.members.is_empty() and not survivor.is_terminal, "group validates all input before subscribing or cancelling")
		invalid.pause()
		invalid.resume()
		invalid.cancel()
	var rejected := scheduler.add(RefCounted.new(), T.value(0.0, 1.0, -1.0))
	var rejected_reason: int = await T.group([rejected]).wait()
	check(rejected_reason == T.Reason.FAILED, "group of rejected handles is immediately awaitable")
	if OS.has_feature("web"):
		scheduler.dispose()
		return # Worker-thread rejection is covered on desktop; the Web preset is single-threaded.
	_host.failures.append_array(_host._collector.take_errors())
	var worker := Thread.new()
	check(worker.start(func() -> TweensGdGroup: return T.group([survivor])) == OK, "worker thread starts")
	var worker_group: TweensGdGroup = worker.wait_to_finish()
	var worker_diagnostics: Array[String] = _host._collector.take_errors()
	var worker_failed: bool = worker_group.is_settled and await worker_group.wait() == T.Reason.FAILED
	check(worker_failed, "worker group creation returns failed group")
	check(worker_diagnostics.size() == 1 and not survivor.is_terminal, "worker rejection does not touch member state")
	scheduler.dispose()
