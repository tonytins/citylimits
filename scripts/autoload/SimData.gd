extends Node

var city_name: String
var year: int = 1
var prev_quarter: int
var quarter: int = 1
var population: int = 0
var news_ticker: String

var budget: int
var prev_budget: int

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
