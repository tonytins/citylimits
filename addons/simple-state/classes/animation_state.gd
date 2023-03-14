@icon("../icons/animation_state.png")
class_name AnimationState
extends State

## Plays an animation from the linked [member animation_player].
## The name of the animation to be played comes from the name of the node.

## Emitted when the animation started by this state has finished playing.
signal animation_finished


@export_range(0, 20, 1, "or_greater")
## How many times to play before emitting [signal State.choose_new_substate_requested].
## [b]If set to zero, it will go forever.[/b]
var loops := 0


var _loops_left := 0


func _init() -> void:
	set_meta(&"description", "Plays the named animation from the linked AnimationPlayer.")


func _on_animation_finished(animation_name: StringName) -> void:
	if animation_name != name: return
	if loops == 0:
		animation_player.play(name)
	elif _loops_left <= 0:
		choose_new_substate_requested.emit()
	else:
		_loops_left -= 1
		animation_player.play(name)


func enter(set_target: Node, set_animation_player: AnimationPlayer, set_debug_mode := false) -> void:
	super(set_target, set_animation_player, set_debug_mode)
	assert(animation_player != null, "AnimationPlayer must be set, either directly or by an ancestor.")
	animation_player.animation_finished.connect(_on_animation_finished)
	_loops_left = loops - 1
	animation_player.play(name)


func exit() -> void:
	super()
	animation_player.animation_finished.disconnect(_on_animation_finished)
	animation_player.stop()
