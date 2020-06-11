extends Node

var city_name: String

var budget: int
var prev_budget: int

var res_tax: int
var comm_tax: int
var indust_tax: int

var fire_tax: int
var police_tax: int
var power_tax: int

func starting_budget(lev: int):
	
	if lev == 1 or lev == 0:
		budget = 20000
	elif lev == 2:
		budget = 10000
	elif lev == 3:
		budget = 5000
	else:
		budget = NAN
