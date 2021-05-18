extends Camera2D

export var panSpeed = 10.0
export var speed = 25.0
export var zoomspeed = 50.0
export var zoommargin = 0.3

export var zoomMin = 0.5
export var zoomMax = 3.0
export var marginX = 200.0
export var marginY = 100.0

var mousepos = Vector2()
var zoompos = Vector2()
var zoomfactor = 1.0


func _ready():
	pass 


func _process(delta):
	# Smooth Movement
	var inputx = (int(Input.is_action_pressed("ui_right")) - int(Input.is_action_pressed("ui_left")))
	var inputy = (int(Input.is_action_pressed("ui_down")) - int(Input.is_action_pressed("ui_up")))
	position.x = lerp(position.x, position.x + inputx * speed * zoom.x, speed * delta)
	position.y = lerp(position.y, position.y + inputy * speed * zoom.y, speed * delta)
	
	# Edge scrolling via. Ctrl + Right Click
	if Input.is_key_pressed(KEY_CONTROL):
		# check mouse postion
		if mousepos.x < marginX:
			position.x = lerp(position.x, position.x - abs(mousepos.x - marginX) / marginX * panSpeed * zoom.x, panSpeed * delta)
		elif mousepos.x > OS.window_size.x - marginX:
			position.x = lerp(position.x, position.x + abs(mousepos.x - OS.window_size.x + marginX) / marginX * panSpeed * zoom.x, panSpeed * delta)
		
		if mousepos.y < marginY:
			position.y = lerp(position.y, position.y - abs(mousepos.y - marginY) / marginY * panSpeed * zoom.y, panSpeed * delta)
		elif mousepos.y > OS.window_size.y - marginY:
			position.y = lerp(position.y, position.y + abs(mousepos.y - OS.window_size.y + marginX) / marginX * panSpeed * zoom.y, panSpeed * delta)
		
	# Zooming
	zoom.x = lerp(zoom.x, zoom.x * zoomfactor, zoomspeed * delta)
	zoom.y = lerp(zoom.y, zoom.y * zoomfactor, zoomspeed * delta)
	
	zoom.x = clamp(zoom.x, zoomMin, zoomMax)
	zoom.y = clamp(zoom.y, zoomMin, zoomMax)

func _input(event):
	if abs(zoompos.x - get_global_mouse_position().x) > zoommargin:
		zoomfactor = 1.0
	if abs(zoompos.y - get_global_mouse_position().y) > zoommargin:
		zoomfactor = 1.0
	
	if event is InputEventMouseButton:
		if event.is_pressed():
			if event.button_index == BUTTON_WHEEL_UP:
				zoomfactor -= 0.01
				zoompos = get_global_mouse_position()
			if event.button_index == BUTTON_WHEEL_DOWN:
				zoomfactor += 0.01
				zoompos = get_global_mouse_position()
				
	if event is InputEventMouse:
		mousepos = event.position
