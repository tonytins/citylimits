extends KinematicBody2D

signal grabbed
signal power

export var cost: int = 10000
export var income: int
export var expense: int

onready var zone = $Sprite
onready var quarters = $Quarters
onready var player = $AnimationPlayer

var can_grab = false
var is_powered = false
var grabbed_offset = Vector2()

func _ready():
	SimEvents.connect("pay_expense", self, "_get_expense")
	connect("grabbed", self, "_grab_zone")
	connect("power", self, "_power_zone")

func _input(event):
	if event is InputEventMouseButton and can_grab:
		# Disable grabbing
		can_grab = false
		grabbed_offset = position - get_global_mouse_position()
		
		# Substract from the player's budget
		if SimData.budget >= cost:
			SimData.budget -= cost

func _process(delta):	
	if can_grab:
		position = get_global_mouse_position() + grabbed_offset
	
	if not is_powered:
		quarters.stop()
	else:
		quarters.start()
	
func _grab_zone():
	can_grab = true
	
func _power_zone():
	quarters.start()
	player.play("Animante")

func _on_Quarters_timeout():
	if SimData.prev_quarter == 4:
		if SimData.budget >= expense:
			SimData.budget -= expense
		
		if SimData.budget >= income:
			SimData.budget += income
		
		SimData.year += 1
		SimData.emit_signal("one_time_zone")
		SimData.emit_signal("pay_expense")
		
		SimData.prev_quarter = SimData.quarter
		
	quarters.start()
