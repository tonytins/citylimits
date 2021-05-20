extends Node

onready var gui = $Controls/GUI
onready var advisor = $Controls/GUI/AdvsiorNotice

# Called when the node enters the scene tree for the first time.
func _ready():
	advisor.show()
