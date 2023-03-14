extends State


const NEXT_STATE_ACTION = "demo_next_state"


func _enter() -> void:
	if InputMap.has_action(NEXT_STATE_ACTION):
		return
	var input_event := InputEventKey.new()
	input_event.keycode = KEY_TAB
	
	InputMap.add_action(NEXT_STATE_ACTION)
	InputMap.action_add_event(NEXT_STATE_ACTION, input_event)
