extends AcceptDialog

var dialogue_file
# "character" would be "name" but it's already used by the base class
var character
var rank
var avatar

var dialogue_keys = []
var dialogue_name = ""
var dialogue_text = ""

onready var avatar_texture = $Container/Advisor/Avatar
onready var rank_label = $Container/Advisor/RankLbl
onready var name_label = $Container/Advisor/NameLbl
onready var description_label = $Container/DescriptionLbl

func _start_dialogue(advisor, message):
	if advisor == SimData.Advisors.CITY_PLANNER:
		dialogue_file = "res://dialog/cityplanner.json"
		_load_dialogue()
		avatar_texture.texture = preload("res://sprites/avatars/zc.png")
		name_label.text = "Zack Casey"
		rank_label.text = "City Planner"
	elif advisor == SimData.Advisors.FINANCIAL:
		dialogue_file = "res://dialog/finacial.json"
		_load_dialogue()
		avatar_texture.texture = preload("res://sprites/avatars/kit.png")
		name_label.text = "Kit Welsh"
		rank_label.text = "Financial Advisor"
		
	_index_dialogue()
	description_label.text = dialogue_keys[message].text
	window_title = dialogue_keys[message].name
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
	SimEvents.connect("advisor_message", self, "_start_dialogue")
