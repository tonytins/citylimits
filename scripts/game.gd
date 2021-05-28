extends Node2D

onready var rotate_news = $RotateNews
onready var day_cycle = $DayCycle
onready var turtle_btn = $Controls/Status/StatCtr/ButtonCtr/TurtleBtn
onready var cheeta_btn = $Controls/Status/StatCtr/ButtonCtr/CheetaBtn

func _ready():
	SimEvents.connect("resume_news", self, "_resume_rotation")
	SimEvents.connect("send_alert", self, "_stop_news")
	
func _stop_news():
	rotate_news.stop()
	
func _resume_rotation():
	rotate_news.start()

func _on_DayCycle_timeout():
	
	if SimData.prev_month < 12:
		SimData.last_total_days = SimData.total_days
		SimData.total_days += 1
	
	# Increment the number days until it reaches 30
	if SimData.prev_day < 30:
		SimData.day += 1
	
	# Reset the number of days to 1 on day 30 and increment the month
	if SimData.prev_day == 30:
		SimData.day = 1
		SimData.prev_month = SimData.month
		SimData.month += 1
		SimEvents.emit_signal("budget")
	
	# Increment the year on the 12th month 
	if SimData.prev_month == 12:
		SimData.prev_year = SimData.year
		SimData.total_days = 1
		SimData.month = 1
		SimData.year += 1
	
	SimData.last_total_days = SimData.total_days
	SimData.prev_day = SimData.day

func _on_TurtleBtn_toggled(button_pressed):
	if button_pressed:
		day_cycle.wait_time = 12
		cheeta_btn.pressed = false

func _on_CheetaBtn_toggled(button_pressed):
	if button_pressed:
		day_cycle.wait_time = 2
		turtle_btn.pressed = false
		
