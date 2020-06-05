extends Control

func _on_CreateBtn_pressed():
	var city_name = $CityNameEdit.text
	CityData.city_name = city_name
	
	get_tree().change_scene("res://src/game.tscn")
