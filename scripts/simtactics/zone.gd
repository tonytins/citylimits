extends ConditionLeaf

export var cost: int = 10000
export var income: int = 100
export var expense: int = 0

func tick(actor, blackboard):
	if SimData.has_power:
		if SimData.budget >= expense:
			SimData.budget -= expense
			SimData.expenses = expense
			
		if SimData.has_power:
			var total_income = SimData.res_tax * income
			SimData.budget += total_income
			SimData.res_income = total_income
			
	return RUNNING
