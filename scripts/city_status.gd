extends HBoxContainer

onready var city_name = $CityNameLbl
onready var budget = $BudgetLbl
onready var year = $YearCtr/YearLbl

func _process(delta):
	city_name.text = SimData.city_name
	budget.text = SimData.currency + str(SimData.budget)
	year.text = str(SimData.year)
