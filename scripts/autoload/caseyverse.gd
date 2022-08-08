extends Node

const caseyverse_path = "res://json/ticker/extra_lore.json"
const news_outlets = "res://json/ticker/extra_lore.json"
const is_caseyverse_path = "res://is_caseyverse.txt"

func is_caseyverse():
	var file = File.new()
	if file.file_exists(is_caseyverse_path):
		return true
