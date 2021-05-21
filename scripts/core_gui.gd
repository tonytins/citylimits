extends Control

onready var debug_console = $Console
onready var advisor = $AdvsiorNotice
# onready var news_ticker = $TickerPanel/ScrollContainer

func _ready():
	advisor.show()

func _process(delta):
	if Input.is_action_pressed("ui_cheats"):
		debug_console.show()
