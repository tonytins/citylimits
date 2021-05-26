extends Node

var city_name: String = "Furtropolis" # Hard-coded, for now
var mayor_name: String = "Defecto"
var year: int = 1980
var prev_quarter: int
var quarter: int = 1
var population: int = 0
var budget: int = 20000
var expenses: int
var is_alert: bool = false
var has_ctower: bool = false # Central Tower

const currency: String = "§"

var power_grid: int # Number of power stations in the area. Helps provide redundancies.
var power_capacity: int
var current_power_cap: int
var prev_power_cap: int
var has_power: bool

var res_tax: int = 1
var comm_tax: int = 1
var indust_tax: int = 1

var res_income: int
var comm_income: int
var ind_income: int

var fire_tax: int
var police_tax: int
var power_tax: int

enum GameSpeed {
	SLOW, 
	MEDIUM, 
	FAST
}

enum Advisors { 
	CITY_PLANNER,
	FINANCIAL
}

func starting_budget(lev: int):
	
	if lev == 1 or lev == 0:
		budget = 20000
	elif lev == 2:
		budget = 10000
	elif lev == 3:
		budget = 5000
	else:
		budget = NAN
