extends Node2D

onready var rotate_news = $RotateNews
onready var quarters = $Quarters

func _ready():
	SimEvents.connect("resume_news", self, "_resume_rotation")
	SimEvents.connect("send_alert", self, "_stop_news")

func _on_Quarters_timeout():
	SimData.year += 1
	SimData.prev_quarter = SimData.quarter
	
	SimEvents.emit_signal("budget")
	
func _stop_news():
	rotate_news.stop()
	
func _resume_rotation():
	rotate_news.start()
