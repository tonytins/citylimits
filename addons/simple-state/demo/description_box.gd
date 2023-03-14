extends Label


## Mode of description box rendering.
enum DisplayModes { 
		NONE, ## Description box completely hidden.
		ACTIVE, ## Show descriptions of all active states.
		SELECTION, ## Show description of last selected state (also includes manual switches).
		}


@export
## [StateMachineDebugger] to reference.
var tree : StateMachineDebugger:
	set(value):
		tree = value
		if show_descriptions == DisplayModes.SELECTION and \
				not tree.item_selected.is_connected(_on_tree_item_selected):
			tree.item_selected.connect(_on_tree_item_selected)
			

@export
## Show a description of a state.
## Looks for a string metadata value by the name of [code]description[/code] on each state.
var show_descriptions := DisplayModes.NONE:
	set(value):
		show_descriptions = value
	
		match show_descriptions:
			DisplayModes.NONE:
				visible = false
			DisplayModes.ACTIVE:
				visible = true
				if not is_instance_valid(tree):
					return
				if tree.item_selected.is_connected(_on_tree_item_selected):
					tree.item_selected.disconnect(_on_tree_item_selected)
			DisplayModes.SELECTION:
				visible = true
				if not is_instance_valid(tree):
					return
				if not tree.item_selected.is_connected(_on_tree_item_selected):
					tree.item_selected.connect(_on_tree_item_selected)


var _active_states : Array[State] = []


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	visible = show_descriptions != DisplayModes.NONE
	connect_signals()


func connect_signals(state := tree.state_machine_root) -> void:
	if not state.has_meta(&"tree_item"):
		return
	for signal_name in tree.signal_connections:
		if state.has_signal(signal_name) and not \
				state.is_connected(signal_name, _on_state_signal):
			state.connect(signal_name, _on_state_signal.bind(signal_name, state))
	for child in(state.get_children() as Array[State]):
		connect_signals(child)


func disconnect_signals(state := tree.state_machine_root) -> void:
	for signal_name in tree.signal_connections:
		if state.has_signal(signal_name) and \
				state.is_connected(signal_name, _on_state_signal):
			state.disconnect(signal_name, _on_state_signal)
	for child in (state.get_children() as Array[State]):
		disconnect_signals(child)


func _set_description_from_active_states() -> void:
	text = ""
	for state in _active_states:
		text += ("" if state.is_root() else "\n\n") + \
				state.name as String + \
				": " + \
				state.get_meta(&"description", "") as String


func _on_tree_item_selected() -> void:
	text = tree.get_selected() \
			.get_metadata(0).get_meta(&"description", "")


func _on_state_signal(signal_name: StringName, state: State) -> void:
	match signal_name:
		&"entered":
			if show_descriptions == DisplayModes.ACTIVE:
				_active_states.push_back(state)
				_set_description_from_active_states()
		&"exited":
			if show_descriptions == DisplayModes.ACTIVE:
				_active_states.pop_back()
				_set_description_from_active_states()
