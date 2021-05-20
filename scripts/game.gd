extends Node2D

onready var quarters = $Quarters

func _on_Quarters_timeout():
	SimData.year += 1
	SimData.prev_quarter = SimData.quarter
	
	SimEvents.emit_signal("budget")	
	quarters.start()
