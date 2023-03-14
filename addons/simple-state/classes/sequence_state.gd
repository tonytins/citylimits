@icon("../icons/sequence_state.png")
class_name SequenceState
extends State


## Executes its children in order, one after the other. Like an [Array] in [State] form!


@export_range(0, 20, 1, "or_greater")
## How many times the sequence should be looped through before emitting [signal State.choose_new_substate_requested].
## [b]If set to zero, it will go forever.[/b]
var loops := 1


var _loops_left := 0


func _init() -> void:
	set_meta(&"description", "Starts its children one after the other in order, \
			waiting for each one to be done before starting the next.")


# You can define which state is picked automatically (like on [method enter]).
# If you would like to call it yourself, use the public version ([method choose_substate]).
func _choose_substate() -> State:
	if _active_substate == null:
		return get_child(0) as State if get_child_count() > 0 else null
	
	if _active_substate.get_index() == get_child_count() - 1:
		if loops == 0:
			return get_child(0) as State
		elif _loops_left == 0:
			choose_new_substate_requested.emit()
			return null
		else:
			_loops_left -= 1
			return get_child(0) as State
	return get_child(_active_substate.get_index() + 1) as State


func enter(set_target: Node, set_animation_player: AnimationPlayer, set_debug := false) -> void:
	super(set_target, set_animation_player, set_debug)
	_loops_left = loops - 1
