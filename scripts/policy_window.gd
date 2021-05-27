extends WindowDialog

var dialogue_file = "res://dialog/policies/policies.json"

var dialogue_keys = []
var dialogue_name = ""
var dialogue_text = ""
var policy: int

onready var ordinance_label = $OrdinanceLbl
onready var description_label = $DescPanel/DescriptionLbl

func _start_dialogue(ordinance):
	
	_index_dialogue()
	
	match ordinance:
		1:
			policy = 1
		2:
			policy = 2
		3:
			policy = 3
	
	var proposel: String = dialogue_keys[policy].text
	
	if "[name]" in proposel:
		proposel = proposel.replace("[name]", SimData.mayor_name)
	
	if "[city]" in proposel:
		proposel = proposel.replace("[city]", SimData.city_name)
			
	description_label.text = proposel
	ordinance_label.text = dialogue_keys[policy].name
	
	show()

func _index_dialogue():
	var dialogue = _load_dialogue()
	dialogue_keys.clear()
	for key in dialogue:
		dialogue_keys.append(dialogue[key])

func _load_dialogue():
	var file = File.new()
	if file.file_exists(dialogue_file):
		file.open(dialogue_file, file.READ)
		var dialogue = parse_json(file.get_as_text())
		return dialogue

func _ready():
	SimEvents.connect("policy_message", self, "_start_dialogue")

func _on_IgnoreBtn_pressed():
	policy = 0
	description_label.text = ""
	ordinance_label.text = ""
	hide()

func _on_EnectBtn_pressed():
	match policy:
		1: SimEvents.emit_signal("energy_saving")
		2: SimEvents.emit_signal("clean_air_act")

func _on_AnalysisBtn_pressed():
	match policy:
		1: SimEvents.emit_signal("policy_analysis", SimData.Advisors.CITY_PLANNER, policy)
		2: SimEvents.emit_signal("policy_analysis", SimData.Advisors.CITY_PLANNER, policy)
		3: SimEvents.emit_signal("policy_analysis", SimData.Advisors.CITY_PLANNER, policy)
