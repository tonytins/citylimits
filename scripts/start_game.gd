extends Panel

onready var city_name = $Container/CityNameEdit
onready var mayor_name = $Container/MayorNameEdit
onready var budget = $Container/BudgetMenu

func _ready():
	city_name.text = SimData.city_name
	mayor_name.text = SimData.mayor_name

func _on_CreateBtn_pressed():
	SimData.city_name = city_name.text
	SimData.mayor_name = mayor_name.text
	
	get_tree().change_scene("res://scenes/Game.tscn")
