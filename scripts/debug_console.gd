extends Control

onready var input_box = $Input
onready var output_box = $Output

func _ready():
	input_box.grab_focus()

func output_text(text):
	output_box.text = text + "\n"

func _on_Input_text_entered(new_text):
	input_box.clear()
	output_text(new_text)
