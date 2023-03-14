extends AnimationState


func _update(_delta: float) -> void:
	if Input.is_action_just_pressed(get_root().NEXT_STATE_ACTION):
		choose_new_substate_requested.emit()
