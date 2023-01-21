extends Control

onready var debug_console = $Console
onready var tax_window = $TaxWindow
onready var advsior_meet_window = $AdvisorMeet
onready var tools_window = $ToolsWindow

onready var verLabel = $VersionLbl

func _ready():
	# To get version string
	var version = ProjectSettings.get_setting("application/config/version")
	# To get build number
	var build = ProjectSettings.get_setting("application/config/build")
	
	verLabel.text = version + " (Build " + str(build) + ")"
	
	SimData.city_name = SimData.city_name.capitalize()
	SimData.mayor_name = SimData.mayor_name.capitalize()
	
	SimEvents.emit_signal("advisor_message", SimData.Advisors.CITY_PLANNER, 0)
	
	SimEvents.connect("send_alert", self, "_start_alert")
	SimEvents.connect("rotate_news", self, "_rotate_news")

func _process(delta):
	if Input.is_action_pressed("ui_cheats"):
		debug_console.show()

