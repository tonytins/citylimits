extends Control

onready var debug_console = $Console
onready var advisor = $AdvsiorNotice

func _ready():
	advisor.show()

func _process(delta):
	if Input.is_action_just_released("ui_cheats"):
		debug_console.show()
