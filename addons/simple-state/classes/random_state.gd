@icon("../icons/random_state.png")
class_name RandomState
extends State

## Activates a random one of its substates.
## Useful in conjuction with [AnimationState] for random idles.


@export
## When one of its children asks for a state change,
## instead of picking another one itself, it defers that choice to its parent.
## Allows for nested random states for finer control over flow and probability.
var defer_choice := false


func _init() -> void:
	set_meta(&"description", "Pseudo-randomly picks a state to start.")


func _ready() -> void:
	randomize()
	super()


# You can define which state is picked automatically (like on enter).
# If you would like to call it yourself, use the public version (choose_substate).
func _choose_substate() -> State:
	if get_child_count() == 0: 
		return null
	if defer_choice and _active_substate != null: 
		choose_new_substate_requested.emit()
		return null
	return get_child(randi() % get_child_count()) as State

