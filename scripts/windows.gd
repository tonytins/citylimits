extends Control

onready var debug_console = $Console
onready var tax_window = $TaxWindow
onready var advsior_meet_window = $AdvisorMeet
onready var tools_window = $ToolsWindow

func _ready():
	SimData.city_name = SimData.city_name.capitalize()
	SimData.mayor_name = SimData.mayor_name.capitalize()
	
	SimEvents.emit_signal("advisor_message", SimData.Advisors.CITY_PLANNER, 0)
	
	SimEvents.connect("send_alert", self, "_start_alert")
	SimEvents.connect("rotate_news", self, "_rotate_news")

func _process(delta):
	if Input.is_action_pressed("ui_cheats"):
		debug_console.show()

