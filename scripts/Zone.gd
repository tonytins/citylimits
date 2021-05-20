extends KinematicBody2D

signal grabbed

export var cost: int = 10000
export var income: int
export var expense: int
enum IsPowerStation {TRUE, FALSE}
export(IsPowerStation) var power_station = IsPowerStation.FALSE

onready var zone = $Sprite
onready var quarters = $Quarters
onready var animator = $AnimationPlayer

var can_grab = false
var grabbed_offset = Vector2()

func _ready():
	SimEvents.connect("budget", self, "_get_budget")
	SimEvents.connect("has_power", self, "_power_zone")
	connect("grabbed", self, "_grab_zone")

func _input(event):
	if event is InputEventMouseButton and can_grab:
		# Disable grabbing
		can_grab = false
		grabbed_offset = position - get_global_mouse_position()
		
		# Substract from the player's budget
		if SimData.budget >= cost:
			SimData.budget -= cost
			
		if power_station == IsPowerStation.TRUE:
			SimEvents.emit_signal("has_power")

func _process(delta):	
	if can_grab:
		position = get_global_mouse_position() + grabbed_offset
	
	if SimData.has_power and can_grab == false:
		quarters.start()
		_animante_sprite()
	else:
		quarters.stop()
		_animante_sprite(false)
		
func _animante_sprite(animante: bool = true):
	if zone.hframes > 1 or zone.vframes > 1 and animante:
			animator.play("Animante")
	else:
			animator.stop()
				
func _grab_zone():
	can_grab = true
	
func _power_zone():
	SimData.has_power = true

func _get_budget():
	if SimData.budget >= expense:
		SimData.budget -= expense
		
	if SimData.budget >= income:
		SimData.budget += income
