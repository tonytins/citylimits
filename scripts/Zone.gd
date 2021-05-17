extends KinematicBody2D

export var cost: int = 10000
export var income: int
export var expense: int

onready var zone = $Sprite
onready var quarters = $Quarters
onready var player = $AnimationPlayer

var can_grab = false
var grabbed_offset = Vector2()

func _ready():
	SimEvents.connect("pay_expense", self, "_get_expense")
	
	# Once placed in-world, it'll substract from your budget
	if SimData.budget >= cost:
		SimData.budget -= cost

func _input(event):
	if event is InputEventMouseButton:
		can_grab = event.pressed
		grabbed_offset = position - get_global_mouse_position()

func _process(delta):
	
	player.play("Animante")
	
	if SimData.budget >= expense:
		SimData.budget -= expense
		
	if SimData.budget >= income:
		SimData.budget += income
	
	if Input.is_mouse_button_pressed(BUTTON_LEFT) and can_grab:
		position = get_global_mouse_position() + grabbed_offset

func _on_Quarters_timeout():
	if SimData.prev_quarter == 4:
		SimData.year += 1
		SimData.emit_signal("one_time_zone")
		SimData.emit_signal("pay_expense")
		
	SimData.prev_quarter = SimData.quarter
	quarters.start()
