extends Area2D

onready var zone = $Sprite
onready var quarters = $Quarters
onready var animator = $AnimationPlayer

var can_grab = false
var grabbed_offset = Vector2()

func _ready():
	zone.frame = 0
	
#func _drag_drop(event):
#	if event is InputEventMouseButton and can_grab:
#		# Substract from the player's budget and disable grabbing
#		if SimData.budget >= cost:
#			SimData.budget -= cost
#			can_grab = false
#			grabbed_offset = position - get_global_mouse_position()

#func _input(event):
#	_drag_drop(event)

func _process(delta):	
	if SimData.has_power == true:
			animator.play("Animante")
	else:
			animator.stop()
#	if can_grab:
#		position = get_global_mouse_position() + grabbed_offset
		
#func _animante_sprite(animante: bool = true):
#	if zone.hframes > 1 or zone.vframes > 1 and animante:
#			animator.play("Animante")
#	else:
#			animator.stop()
				
#func _grab_zone():
#	can_grab = true
