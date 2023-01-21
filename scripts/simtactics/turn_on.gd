extends ConditionLeaf

func tick(actor, blackboard):
	SimData.has_power = true
	return RUNNING
