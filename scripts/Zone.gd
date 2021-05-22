extends KinematicBody2D

signal grabbed

export var cost: int = 10000
export var income: int = 100
export var expense: int = 0

onready var zone = $Sprite
onready var quarters = $Quarters
onready var animator = $AnimationPlayer

var can_grab = false
var grabbed_offset = Vector2()

func _ready():
	SimEvents.connect("budget", self, "_get_budget")
	connect("grabbed", self, "_grab_zone")

func _drag_drop(event):
	if event is InputEventMouseButton and can_grab:
		# Substract from the player's budget and disable grabbing
		if SimData.budget >= cost:
			SimData.budget -= cost
			can_grab = false
			grabbed_offset = position - get_global_mouse_position()

func _input(event):
	_drag_drop(event)

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

func _get_budget():
	if SimData.budget >= expense and SimData.has_power:
		SimData.budget -= expense
		SimData.expenses = expense
		
	if SimData.has_power:
		var total_income = SimData.res_tax * income
		SimData.budget += total_income
		SimData.res_income = total_income
