extends Node

var city_name: String = "Furtropolis" # Hard-coded, for now
var mayor_name: String = "Defecto"
var population: int = 0
var budget: int = 20000
var expenses: int
var on_alert: bool = false
var has_ctower: bool = false # Central Tower

var year: int = 2000
var prev_year: int
var month: int = 1
var prev_month: int
var day: int = 1
var prev_day: int
var last_total_days: int
var total_days: int = 1

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

enum Level {
	EASY,
	MEDIUM,
	HARD
}

enum Advisors { 
	CITY_PLANNER,
	FINANCIAL,
	TRANSPORT
}

enum Ordinances {
	# Education
	CPR_TRAINING,
	PRO_READING,
	NHOOD_WATCH,
	# Financial
	PARKING_FINES,
	GAMBLING,
	SALES_TAX,
	# Promo
	ANNUAL_CARNIVAL,
	BUSINESS_ADS,
	CITY_BEAUTY,
	TOURIST_ADS
	# Health & Safety
	FREE_CLINICS,
	JUNIOR_SPORTS,
	SMOKING_BAN,
	VOLUNTEER_FIRE,
	SMOKE_DETECTOR,
	# Environment
	ENERGY_CONSERVATION,
	HOMELESS_SHELTERS,
	CLEAN_AIR_ACT,
	TIRE_RECYCLE
}

#func starting_budget(lev = Level.EASY):
#	match lev:
#		Level.EASY:
#			budget = 20000
#		Level.MEDIUM:
#			budget = 10000
#		Level.HARD:
#			budget = 5000
