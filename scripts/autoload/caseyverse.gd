extends Node

const caseyverse_path = "res://json/ticker/extra_lore.json"
const is_caseyverse_path = "res://is_caseyverse.txt"

func is_caseyverse():
	var file = File.new()
	if file.file_exists(is_caseyverse_path):
		return true
		
func competing_outlet():
	var file = File.new()
	if is_caseyverse():
		file.open(caseyverse_path, File.READ)
		var result = parse_json(file.get_as_text())
		return result["competing_outlet"]
