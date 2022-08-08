extends Node

func key_value(json_path, json_file, key, is_dictionary = false):
	var file = File.new()
	var full_path = str(json_path + json_file);	
	if file.file_exists(full_path):
		file.open(full_path, File.READ)
		var result = parse_json(file.get_as_text())
		if is_dictionary == true:
			result.clear()
		return result[key]
