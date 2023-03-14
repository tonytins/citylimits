@icon("../icons/state_machine_debugger.png")
class_name StateMachineDebugger
extends Tree


## Displays an interactive state tree.
# This source code is a mess, I'm trying to make it less so.


@export
## Root state machine to reference.
var state_machine_root : State:
	set(value):
		state_machine_root = value
		_setup_tree()

@export
## What color to make the item when a state is active.
var active_color := Color.FOREST_GREEN

@export
## Forcefully switch states by double-clicking them.
## Due to its nature, it has the potential to be destructive
## and/ or not behave completely how one might expect.
var allow_state_switching := false:
	set(value):
		allow_state_switching = value
		if allow_state_switching:
			item_activated.connect(_on_item_activated)
		else:
			item_activated.disconnect(_on_item_activated)


@export_group("Signals", "signal_")
@export
## Show when a state emits a relevant signal.
var signal_show := false:
	set(value):
		signal_show = value
		
		if state_machine_root == null:
			return
		
		if signal_show:
			connect_signals()
		else:
			disconnect_signals()

@export
## Which signals to connect to on each state, as long as they exist.
var signal_connections : Array[StringName] = [
	&"entered",
	&"exited",
	&"choose_new_substate_requested",
	&"animation_finished",
]

@export
## Delay before hiding signal.
var signal_hide_delay := 1.0


func _init() -> void:
	columns = 2


func change_state_by_path(path: NodePath) -> void:
	if not state_machine_root.has_node(path):
		return
	var state := state_machine_root
	for i in path.get_name_count():
		var part := path.get_name(i)
		state = await state.change_state_name(part)


func connect_signals(state := state_machine_root) -> void:
	if not state.has_meta(&"tree_item"):
		return
	for signal_name in signal_connections:
		if state.has_signal(signal_name) and not \
				state.is_connected(signal_name, _on_state_signal):
			state.connect(signal_name, _on_state_signal.bind(signal_name, state.get_meta(&"tree_item")))
	for child in(state.get_children() as Array[State]):
		connect_signals(child)


func disconnect_signals(state := state_machine_root) -> void:
	for signal_name in signal_connections:
		if state.has_signal(signal_name) and \
				state.is_connected(signal_name, _on_state_signal):
			state.disconnect(signal_name, _on_state_signal)
	for child in (state.get_children() as Array[State]):
		disconnect_signals(child)


func _setup_tree(state := state_machine_root, parent_item: TreeItem = null) -> void:
	if state == state_machine_root:
		if get_root() != null:
			disconnect_signals()
		clear()
		if state_machine_root == null:
			return
#		state.print_tree_pretty()
	
	# TODO: add icons
	var item := create_item(parent_item)
	item.set_text(0, state.name)
	item.set_metadata(0, state)
	state.set_meta(&"tree_item", item)
	connect_signals(state)
	
	for child in (state.get_children() as Array[State]):
		_setup_tree(child, item)


func _on_item_activated() -> void:
	change_state_by_path(state_machine_root.get_path_to(
			get_selected().get_metadata(0) as State))


func _on_state_signal(signal_name: StringName, state_item: TreeItem) -> void:
	match signal_name:
		&"entered":
			for i in columns:
				state_item.set_custom_color(i, active_color)
		&"exited":
			for i in columns:
				state_item.clear_custom_color(i)
	
	if not signal_show:
		return
	
	state_item.set_text(1, signal_name)
	var timer := state_item.get_metadata(1) as SceneTreeTimer
	if timer != null:
		timer.timeout.disconnect(state_item.set_text)
	timer = get_tree().create_timer(signal_hide_delay)
	timer.timeout.connect(state_item.set_text.bind(1, ""))
	state_item.set_metadata(1, timer)
