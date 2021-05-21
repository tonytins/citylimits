extends Node

enum {
	ARG_INT,
	ARG_STRING,
	ARG_BOOL,
	ARG_FLOAT
}

const valid_commands = [
	["money", [ARG_INT] ],
	["whereyoufrom", [ARG_STRING] ]
]

func money(value):
	SimData.budget += int(value)
	return "Budget changed to " + str(value)
	
func whereyoufrom(value):
	SimData.city_name = str(value)
	return "Changed city name to: " + str(value)
