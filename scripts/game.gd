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
	
	if SimData.prev_day < 30:
		SimData.prev_day = SimData.day
		SimData.day += 1
	
	if SimData.prev_day == 30:
		SimData.prev_day = SimData.day
		SimData.day = 1
		SimData.prev_month = SimData.month
		SimData.month += 1
		SimEvents.emit_signal("budget")
		
	if SimData.prev_month == 12:
		SimData.prev_year = SimData.year
		SimData.total_days = 1
		SimData.month = 1
		SimData.year += 1
	
	SimData.prev_day = SimData.day

func _on_TurtleBtn_toggled(button_pressed):
	if button_pressed:
		day_cycle.wait_time = 12
		cheeta_btn.pressed = false

func _on_CheetaBtn_toggled(button_pressed):
	if button_pressed:
		day_cycle.wait_time = 2
		turtle_btn.pressed = false
		
