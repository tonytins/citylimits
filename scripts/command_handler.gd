extends Node

enum {
	ARG_INT,
	ARG_STRING,
	ARG_BOOL,
	ARG_FLOAT
}

const valid_commands = [
	["money", [ARG_STRING] ],
	["whereyoufrom", [ARG_STRING] ],
	["whatyearisit", [ARG_STRING] ]
]

func _budget_print(value: int):
	return "Budget increased to " + str(value)

func money(value):
	var motherlode = 50000
	var rosebud = 1000
	if value == "motherlode":
		SimData.budget += motherlode
		return _budget_print(motherlode)
	
	if value == "rosebud":
		SimData.budget += rosebud
		return _budget_print(rosebud)
		
	return ""
	
func whereyoufrom(value):
	SimData.city_name = str(value)
	return "Changed city name to: " + str(value)

func whatyearisit(value):
	SimData.year = int(value)
	return "Change year to: " + str(value)
