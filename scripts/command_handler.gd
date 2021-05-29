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
	["whatyearisit", [ARG_STRING] ],
	["show_policy", [ARG_INT]],
	["set_month", [ARG_INT]],
	["set_year", [ARG_INT]]
]

func set_month(month):
	SimTime.month = month

func set_year(year):
	SimTime.year = year

func show_policy(policy):
	SimEvents.emit_signal("policy_message", policy)

func _budget_print(value: int):
	return "Budget increased to " + str(value)

func money(value):
	SimData.budget += int(value)
	
func whereyoufrom(value):
	SimData.city_name = str(value)
	return "Changed city name to: " + str(value)

func whatyearisit(value):
	SimData.year = int(value)
	return "Change year to: " + str(value)
