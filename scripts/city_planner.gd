extends "res://scripts/advisor_window.gd"

func _ready():
	SimEvents.connect("city_planner", self, "_start_dialogue")
