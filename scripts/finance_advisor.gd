extends "res://scripts/advisor_window.gd"

func _ready():
	SimEvents.connect("financial_advisor", self, "_start_dialogue")
