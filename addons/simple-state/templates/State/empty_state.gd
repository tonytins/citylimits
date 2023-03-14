# meta-default: true
extends _BASE_


# Called when the state is activated. (parents, then children)
func _enter() -> void:
	pass


# Called after the state is activated. (children, then parents)
func _after_enter() -> void:
	pass


# Called every physics frame (only when the state is active, of course). (parents, then children)
func _update(delta: float) -> void:
	pass


# Called at the end of every physics frame. (children, then parents)
func _after_update(delta: float) -> void:
	pass


# Called before the state is deactivated. (parents, then children)
func _before_exit() -> void:
	pass


# Called when the state is deactivated. (children, then parents)
func _exit() -> void:
	pass

