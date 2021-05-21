extends Control

onready var input_box = $Input
onready var output_box = $Output
onready var command_handler = $CommandHandler

func _ready():
	input_box.grab_focus()

func process_command(text: String):
	var words = text.split(" ")
	words = Array(words)
	
	for i in range(words.count("")):
		words.erase("")
		
	if words.size() == 0:
		return
		
	var cmd_word = words.pop_front()
	
	for cmd in command_handler.valid_commands:
		if cmd[0] == cmd_word:
			if words.size() != cmd[1].size():
				output_text(str('Failure executing command "', cmd_word,
				 '", expected ', cmd[1].size(), ' parameters'))
				return
				
			for i in range(words.size()):
				if not check_type(words[i], cmd[1][i]):
					output_text(str('Failure executing command "', cmd_word, '", parameter ', (i + 1),
								' ("', words[i], '") is of the wrong type'))
					return
			
				output_text(command_handler.callv(cmd_word, words))
				return
			
	output_text(str('Command: "', cmd_word, '" does not exist'))

func check_type(string: String, type):
	if type == command_handler.ARG_INT:
		return string.is_valid_integer()
		
	if type == command_handler.ARG_FLOAT:
		return string.is_valid_float()
		
	if type == command_handler.ARG_STRING:
		return true
		
	if type == command_handler.ARG_BOOL:
		return (string == "true" or string == "false")
	
	return false
	
	
	

func output_text(text):
	output_box.text = str(output_box.text + "\n", text)

func _on_Input_text_entered(new_text):
	input_box.clear()
	process_command(new_text)
