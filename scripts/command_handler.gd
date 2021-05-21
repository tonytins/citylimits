extends Node

enum {
	ARG_INT,
	ARG_STRING,
	ARG_BOOL,
	ARG_FLOAT
}

const valid_commands = [
	["motherlode", [null],
	["whereyoufrom", [ARG_STRING]]
	]
]

func motherlode():
	SimData.budget += 50000
	
func whereyoufrom(city_name):
	SimData.city_name = city_name
