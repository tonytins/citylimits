extends "res://scripts/zone.gd"

func _ready():
	SimEvents.connect("has_power", self, "_power_zones")

func _power_zones():
	SimData.power_grid += 1
	
	if SimData.power_grid <= 1:
		SimData.has_power = true

func _input(event):
	_drag_drop(event)
	
	if can_grab == false:
		SimEvents.emit_signal("has_power")
