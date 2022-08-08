extends Node2D

onready var rotate_news = $RotateNews
onready var day_cycle = $DayCycle
onready var turtle_btn = $Controls/Status/StatCtr/ButtonCtr/TurtleBtn
onready var cheeta_btn = $Controls/Status/StatCtr/ButtonCtr/CheetaBtn

func _ready():
	SimEvents.connect("rotate_news", self, "_rotate_news")
	SimEvents.connect("send_alert", self, "_stop_news")
	
func _stop_news():
	rotate_news.stop()
	
func _resume_rotation():
	rotate_news.start()

func _on_DayCycle_timeout():
	
	# Increment the number days until it reaches 30
	if SimTime.prev_day < 30:
		SimTime.increment_day(1)
	
	# Reset the number of days to 1 on day 30 and increment the month
	if SimTime.prev_day == 30:
		SimTime.reset_day()
		
		# Increment month up until the 12th
		if SimTime.prev_month != 12:
			SimTime.increment_month(1)
			
		SimEvents.emit_signal("budget")
	
	# Increment the year on the 12th month 
	if SimTime.prev_month == 12:
		SimTime.new_year()
		
func _on_TurtleBtn_toggled(button_pressed):
	if button_pressed:
		day_cycle.wait_time = 12
		cheeta_btn.pressed = false

func _on_CheetaBtn_toggled(button_pressed):
	if button_pressed:
		day_cycle.wait_time = 2
		turtle_btn.pressed = false

func _on_VRMode_pressed():
	# get_tree().change_scene("res://scenes/VR Game.tscn")
	pass
