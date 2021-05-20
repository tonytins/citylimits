extends BoxContainer

onready var city_name = $CityNameLbl
onready var money = $Money/MoneyLbl
onready var year = $YearLbl

func _ready():
	city_name.text = SimData.city_name
	
func _process(delta):
	money.text = str(SimData.budget)
	year.text = str(SimData.year)
