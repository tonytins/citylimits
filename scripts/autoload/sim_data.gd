extends Node

# Hard-coded for now
var city_name: String = "Furtropolis"
var year: int = 1980
var prev_quarter: int
var quarter: int = 1
var population: int = 0
var news_ticker: String
var budget: int = 10000000
var prev_budget: int

var power_grid: int
var has_power: bool

var res_tax: int
var comm_tax: int
var indust_tax: int

var fire_tax: int
var police_tax: int
var power_tax: int

enum GameSpeed {SLOW, MEDIUM, FAST}

func starting_budget(lev: int):
	
	if lev == 1 or lev == 0:
		budget = 20000
	elif lev == 2:
		budget = 10000
	elif lev == 3:
		budget = 5000
	else:
		budget = NAN
