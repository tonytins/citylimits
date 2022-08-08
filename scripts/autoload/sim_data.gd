extends Node

const DEFAULT_CITY = "defualt.json"
const SAVE_PATH = "res://json/saves/"

var city_name: String = ""
var mayor_name: String = ""
var population: int = 0
var budget: int = 20000
var expenses: int
var on_alert: bool = false
var has_ctower: bool = false # Central Tower

var power_grid: int # Number of power stations in the area. Helps provide redundancies.
var power_capacity: int
var current_power_cap: int
var prev_power_cap: int
var has_power: bool

var ticker_files: Array = [
	"adverts.json",
	"sammy.json"
]
var prev_ticker_files: Array = []

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

func _ready():
	if city_name == "":
		city_name = JsonHelper.key_value(SAVE_PATH, DEFAULT_CITY, "city")
	
	if mayor_name == "":
		mayor_name = JsonHelper.key_value(SAVE_PATH, DEFAULT_CITY, "mayor")

#func starting_budget(lev = Level.EASY):
#	match lev:
#		Level.EASY:
#			budget = 20000
#		Level.MEDIUM:
#			budget = 10000
#		Level.HARD:
#			budget = 5000
