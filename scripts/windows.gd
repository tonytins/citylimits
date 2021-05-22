extends Control

onready var debug_console = $Console
onready var tax_window = $TaxWindow
onready var advsior_meet_window = $AdvisorMeet

func _ready():
	SimEvents.emit_signal("city_planner", 0)

func _process(delta):
	if Input.is_action_pressed("ui_cheats"):
		debug_console.show()

func _on_TaxBtn_pressed():
	tax_window.show()

func _on_AdvsiorBtn_pressed():
	advsior_meet_window.show()
