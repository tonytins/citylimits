@tool
extends EditorPlugin


## It uses the icons provided by the scripts anyway, so
## we don't really need to specify the real ones here.
## Plus, it might help with enabling it before the project
## has been reloaded for the first time.
var placeholder_texture := PlaceholderTexture2D.new()


func _enter_tree() -> void:
	add_custom_type("State", "Node", State, placeholder_texture)
	add_custom_type("RandomState", "Node", RandomState, placeholder_texture)
	add_custom_type("AnimationState", "Node", AnimationState, placeholder_texture)
	add_custom_type("SequenceState", "Node", SequenceState, placeholder_texture)
	add_custom_type("StateMachineDebugger", "Tree", StateMachineDebugger, placeholder_texture)


func _exit_tree() -> void:
	remove_custom_type("State")
	remove_custom_type("RandomState")
	remove_custom_type("AnimationState")
	remove_custom_type("SequenceState")
	remove_custom_type("StateMachineDebugger")
