extends HBoxContainer

onready var city_name = $CityNameLbl
onready var budget = $BudgetCtr/BudgetLbl
onready var calendar = $YearCtr/CalendarLbl

func _process(delta):
	city_name.text = SimData.city_name
	budget.text = str(SimData.budget)
	calendar.text = str(SimData.day) + "/" + str(SimData.month) + "/" + str(SimData.year)
