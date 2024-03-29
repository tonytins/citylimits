extends Node2D

var camera_position:Vector2
var camera_rotation:Vector2

@onready var camera = $Camera

func _ready():
	
	# camera_rotation = rotation_degrees # Initial rotation
	
	pass

func _process(delta):
	
	# Set position and rotation to targets
	
	position = position.lerp(camera_position, delta * 10)
	# rotation_degrees = rotation_degrees.lerp(camera_rotation, delta * 6)
	
	handle_input(delta)

# Handle input

func handle_input(_delta):
	
	# Rotation
	
	var input := Vector2.ZERO
	
	input.x = Input.get_axis("camera_left", "camera_right")
	input.y = Input.get_axis("camera_forward", "camera_back")
	
	# input = input.rotated(Vector2.UP, rotation.y).normalized()
	
	camera_position += input / 4
	
	# Back to center
	
	if Input.is_action_pressed("camera_center"):
		camera_position = Vector2()

func _input(event):
	
	# Rotate camera using mouse (hold 'middle' mouse button)
	
	#if event is InputEventMouseMotion:
		#if Input.is_action_pressed("camera_rotate"):
			#camera_rotation += Vector2(0, -event.relative.x / 10, 0)
	pass
