extends AcceptDialog

export(String, FILE, "*.json") var dialogue_file
# "character" would be "name" but it's already used by the base class
export var character: String
export var rank: String
export(Texture) var avatar

var dialogue_keys = []
var dialogue_name = ""
var dialogue_text = ""

onready var avatar_texture = $Container/Advisor/Avatar
onready var rank_label = $Container/Advisor/RankLbl
onready var name_label = $Container/Advisor/NameLbl
onready var description_label = $Container/DescriptionLbl

func _start_dialogue(message):
	_load_dialogue(dialogue_file)
	_index_dialogue()
	description_label.text = dialogue_keys[message].text
	window_title = dialogue_keys[message].name
	show()

func _index_dialogue():
	var dialogue = _load_dialogue(dialogue_file)
	dialogue_keys.clear()
	for key in dialogue:
		dialogue_keys.append(dialogue[key])

func _load_dialogue(file_path):
	var file = File.new()
	if file.file_exists(file_path):
		file.open(file_path, file.READ)
		var dialogue = parse_json(file.get_as_text())
		return dialogue

func _ready():
	avatar_texture.texture = avatar
	name_label.text = character
	rank_label.text = rank
