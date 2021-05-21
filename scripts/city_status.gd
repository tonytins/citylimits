extends BoxContainer

onready var city_name = $CityNameLbl
onready var money = $Money/MoneyLbl
onready var year = $YearLbl

func _process(delta):
	city_name.text = SimData.city_name
	money.text = str(SimData.budget)
	year.text = "Y" + str(SimData.year)
