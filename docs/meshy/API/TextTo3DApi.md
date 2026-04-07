Text to 3D API

Text to 3D API is a feature that allows you to integrate Meshy's Text to 3D capabilities into your own application. In this section, you'll find all the information you need to get started with this API.

The workflow of Text to 3D involves two stages: the preview stage and the refine stage. In the preview stage, a base mesh is generated with no texture applied, allowing you to evaluate the geometry. In the refine stage, the preview mesh is textured based on the text prompt.

POST
/openapi/v2/text-to-3d
Create a Text to 3D Preview Task
This endpoint allows you to create a new Text to 3D Preview task. This task costs 20 credits for Meshy-6 models and 5 credits for other models. Refer to The Text to 3D Task Object to see which properties are included with Text to 3D task object.

Parameters
Required attributes
Name
mode
Type
string
Description
This field should be set to "preview" when creating a preview task.

Name
prompt
Type
string
Description
Describe what kind of object the 3D model is. Maximum 600 characters.

Optional attributes
Name
model_type
Type
string
Description
Specify the type of 3D mesh generation.

Available values:

standard: Regular high-detail 3D mesh generation.
lowpoly: Generates low-poly mesh optimized for cleaner polygons.
Default to standard if not specified.

When lowpoly is selected, ai_model, topology, target_polycount, should_remesh are ignored.

Name
art_style
Type
string
Description
Deprecated. This parameter is designed for legacy models and is not supported by Meshy-6.

Requests using Meshy-6 will ignore art_style, and some combinations may cause errors. Please remove this parameter from your integration. We will remove art_style from the API in a future release.

Available values:

realistic: Realistic style
sculpture: Sculpture style
Default to realistic if not specified.

Note that enable_pbr should be set to false when using Sculpture style, as Sculpture style generates its own set of PBR maps.

Name
ai_model
Type
string
Description
ID of the model to use.

Available values:

meshy-5
meshy-6
latest: Meshy 6
Default to latest if not specified.

Name
topology
Type
string
Description
Specify the topology of the generated model.

Available values:

quad: Generate a quad-dominant mesh.
triangle: Generate a decimated triangle mesh.
Default to triangle if not specified.

Name
target_polycount
Type
integer
Description
Specify the target number of polygons in the generated model. The actual number of polygons may deviate from the target depending on the complexity of the geometry.

The valid value range varies depending on the user tier:

100 to 300,000 (inclusive)
Default to 30,000 if not specified.

Name
should_remesh
Type
boolean
Description
The should_remesh flag controls whether to enable the remesh phase.

When set to false, the API will directly return the highest-precision triangular mesh, ignoring topology and target_polycount.

Set to true if you want to toggle topology and target_polycount, which involves remeshing the initial model input.

Defaults to false for meshy-6 or latest, and true for others if not specified.

Name
symmetry_mode
Type
string
Description
The symmetry_mode field controls symmetry behavior during the model generation process.

The valid values are:

off: Disables symmetry.
auto: Automatically determines and applies symmetry based on input geometry.
on: Enforces symmetry during generation.
Default to auto if not specified.

Name
pose_mode
Type
string
Description
Specify the pose mode for the generated model.

Available values:

a-pose: Generate the model in an A pose.
t-pose: Generate the model in a T pose.
"" (empty string): No specific pose applied.
Default to "" (empty string) if not specified.

Name
is_a_t_pose
Type
boolean
Description
Deprecated. Use pose_mode instead.

Whether to generate the model in an A/T pose.

Default to false if not specified.

Name
moderation
Type
boolean
Description
When set to true, the input content will automatically be screened for potentially harmful content. If harmful content is detected, the task will not proceed to generation.

The text from prompt will be screened.

Defaults to false if not specified.

Name
target_formats
Type
string[]
Description
Specifies which 3D file formats to include in the output. Only the requested formats will be generated and returned, which can reduce task completion time. When omitted, all supported formats are included.

Available values: glb, obj, fbx, stl, usdz, 3mf

3mf is not included in default conversions. You must explicitly include 3mf in target_formats to receive a 3MF file.

Name
auto_size
Type
boolean
Description
When set to true, the service uses AI vision to automatically estimate the real-world height of the object and resize the model accordingly. The origin will default to bottom unless origin_at is explicitly set.

Default to false if not specified.

Name
origin_at
Type
string
Description
Position of the origin when auto_size is enabled.

Available values: bottom, center.

Default to bottom when auto_size is true.

Returns
The result property of the response contains the task id of the newly created Text to 3D task.

Failure Modes
Name
400 - Bad Request
Description
The request was unacceptable. Common causes:

Missing parameter: A required parameter (e.g., prompt, mode) is missing.
Invalid parameter: art_style is not one of the allowed values.
Prompt too long: The prompt exceeds the character limit.
Name
401 - Unauthorized
Description
Authentication failed. Please check your API key.

Name
402 - Payment Required
Description
Insufficient credits to perform this task.

Name
429 - Too Many Requests
Description
You have exceeded your rate limit.

Request
cURL
JavaScript
Python
POST
/openapi/v2/text-to-3d
curl [https://api.meshy.ai/openapi/v2/text-to-3d](https://api.meshy.ai/openapi/v2/text-to-3d "https://api.meshy.ai/openapi/v2/text-to-3d")
-H 'Authorization: Bearer ${YOUR_API_KEY}'
-H 'Content-Type: application/json'
-d '{
"mode": "preview",
"prompt": "a monster mask",
"art_style": "realistic",
"should_remesh": true
}'

Copy
Copied!
Response
{
"result": "018a210d-8ba4-705c-b111-1f1776f7f578"
}

Copy
Copied!
POST
/openapi/v2/text-to-3d
Create a Text to 3D Refine Task
This endpoint allows you to create a new Text to 3D Refine task.

Parameters
Required attributes
Name
mode
Type
string
Description
This field should be set to "refine" when creating a refine task.

Name
preview_task_id
Type
string
Description
The corresponding preview task id.

The status of the given preview task must be SUCCEEDED.

Optional attributes
Name
enable_pbr
Type
boolean
Description
Generate PBR Maps (metallic, roughness, normal) in addition to the base color.

Default to false if not specified.

Note that enable_pbr should be set to false when using Sculpture style, as Sculpture style generates its own set of PBR maps.

Name
texture_prompt
Type
string
Description
Provide an additional text prompt to guide the texturing process. Maximum 600 characters.

Name
texture_image_url
Type
string
Description
Provide a 2d image to guide the texturing process. We currently support .jpg, .jpeg, and .png formats.

There are two ways to provide the image:

Publicly accessible URL: A URL that is accessible from the public internet
Data URI: A base64-encoded data URI of the image. Example of a data URI: data:image/jpeg;base64,`<your base64-encoded image data>`
Image texturing may not work optimally if there are substantial geometry differences between the original asset and uploaded image. Only one of texture_image_url or texture_prompt may be used to guide the texturing process. If both parameters are provided, then texture_prompt will be used to texture the model by default. Texturing via either text or image will cost 10 credits per task.

Name
ai_model
Type
string
Description
ID of the model to use for refining.

Available values:

meshy-5
meshy-6
latest: Meshy 6
Default to latest if not specified.

Name
moderation
Type
boolean
Description
When set to true, the input content will automatically be screened for potentially harmful content. If harmful content is detected, the task will not proceed to generation.

Both the text from texture_prompt and the image from texture_image_url will be screened.

Defaults to false if not specified.

Name
remove_lighting
Type
boolean
Description
Removes highlights and shadows from the base color texture, producing a cleaner result that works better under custom lighting setups.

Default to true if not specified.

Only supported when ai_model is meshy-6 or latest.

Name
target_formats
Type
string[]
Description
Specifies which 3D file formats to include in the output. Only the requested formats will be generated and returned, which can reduce task completion time. When omitted, all supported formats are included.

Available values: glb, obj, fbx, stl, usdz, 3mf

3mf is not included in default conversions. You must explicitly include 3mf in target_formats to receive a 3MF file.

Name
auto_size
Type
boolean
Description
When set to true, the service uses AI vision to automatically estimate the real-world height of the object and resize the model accordingly. The origin will default to bottom unless origin_at is explicitly set.

Default to false if not specified.

Name
origin_at
Type
string
Description
Position of the origin when auto_size is enabled.

Available values: bottom, center.

Default to bottom when auto_size is true.

Returns
The result property of the response contains the task id of the newly created Text to 3D task.

Failure Modes
Name
400 - Bad Request
Description
The request was unacceptable. Common causes:

Invalid task ID: The preview_task_id is invalid or does not exist.
Task not ready: The preview task has not succeeded yet.
Model mismatch: The preview task's AI model is incompatible with the requested refine model.
Name
401 - Unauthorized
Description
Authentication failed. Please check your API key.

Name
402 - Payment Required
Description
Insufficient credits to perform this task.

Name
404 - Not Found
Description
The preview task specified by preview_task_id was not found.

Name
429 - Too Many Requests
Description
You have exceeded your rate limit.

Request
cURL
JavaScript
Python
POST
/openapi/v2/text-to-3d
curl [https://api.meshy.ai/openapi/v2/text-to-3d](https://api.meshy.ai/openapi/v2/text-to-3d "https://api.meshy.ai/openapi/v2/text-to-3d")
-H 'Authorization: Bearer ${YOUR_API_KEY}'
-H 'Content-Type: application/json'
-d '{
"mode": "refine",
"preview_task_id": "018a210d-8ba4-705c-b111-1f1776f7f578",
"enable_pbr": true
}'

Copy
Copied!
Response
{
"result": "018a210d-8ba4-705c-b111-1f1776f7f578"
}

Copy
Copied!
GET
/openapi/v2/text-to-3d/:id
Retrieve a Text to 3D Task
This endpoint allows you to retrieve a Text to 3D task given a valid task id. Refer to The Text to 3D Task Object to see which properties are included with Text to 3D task object.

This endpoint works for both preview and refine tasks.

Parameters
Name
id
Type
path
Description
Unique identifier for the Text to 3D task to retrieve.

Returns
The response contains the Text to 3D task object. Check The Text to 3D Task Object section for details.

Examples
Mode Sample Model
Preview
Refine
Request
cURL
JavaScript
Python
GET
/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578
curl [https://api.meshy.ai/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578](https://api.meshy.ai/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578 "https://api.meshy.ai/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578")
-H "Authorization: Bearer ${YOUR_API_KEY}"

Copy
Copied!
Response
{
"id": "018a210d-8ba4-705c-b111-1f1776f7f578",
"type": "text-to-3d-preview",
"model_urls": {
"glb": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.glb?Expires=***",
"fbx": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.fbx?Expires=***",
"obj": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.obj?Expires=***",
"mtl": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.mtl?Expires=***",
"usdz": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.usdz?Expires=***",
"stl": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.stl?Expires=***"
},
"thumbnail_url": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/preview.png?Expires=***",
"prompt": "a monster mask",
"art_style": "realistic",
"progress": 100,
"started_at": 1692771667037,
"created_at": 1692771650657,
"finished_at": 1692771669037,
"status": "SUCCEEDED",
"texture_urls": [
{
"base_color": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/texture_0.png?Expires=***"
}
],
"preceding_tasks": 0,
"task_error": {
"message": ""
}
}

Copy
Copied!
DELETE
/openapi/v2/text-to-3d/:id
Delete a Text to 3D Task
This endpoint permanently deletes a Text to 3D task, including all associated models and data. This action is irreversible.

Path Parameters
Name
id
Type
path
Description
The ID of the Text to 3D task to delete.

Returns
Returns 200 OK on success.

Request
cURL
JavaScript
Python
DELETE
/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578
curl --request DELETE
--url [https://api.meshy.ai/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578](https://api.meshy.ai/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578 "https://api.meshy.ai/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578")
-H "Authorization: Bearer ${YOUR_API_KEY}"

Copy
Copied!
Response
// Returns 200 Ok on success.

Copy
Copied!
GET
/v2/text-to-3d
List Text to 3D Tasks
This endpoint allows you to retrieve a list of Text to 3D tasks.

Parameters
Optional attributes
Name
page_num
Type
integer
Description
Page number for pagination. Starts and defaults to 1.

Name
page_size
Type
integer
Description
Page size limit. Defaults to 10 items. Maximum allowed is 50 items.

Name
sort_by
Type
string
Description
Field to sort by. Available values:

+created_at: Sort by creation time in ascending order.
-created_at: Sort by creation time in descending order.
Returns
Returns a paginated list of The Text to 3D Task Objects.

Request
cURL
JavaScript
Python
GET
/openapi/v2/text-to-3d
curl [https://api.meshy.ai/openapi/v2/text-to-3d?page_size=10](https://api.meshy.ai/openapi/v2/text-to-3d?page_size=10 "https://api.meshy.ai/openapi/v2/text-to-3d?page_size=10")
-H "Authorization: Bearer ${YOUR_API_KEY}"

Copy
Copied!
Response
[
{
"id": "018a210d-8ba4-705c-b111-1f1776f7f578",
"type": "text-to-3d-preview",
"model_urls": {
"glb": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.glb?Expires=***",
"fbx": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.fbx?Expires=***",
"obj": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.obj?Expires=***",
"mtl": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.mtl?Expires=***",
"usdz": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.usdz?Expires=***"
},
"thumbnail_url": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/preview.png?Expires=***",
"prompt": "a monster mask",
"art_style": "realistic",
"progress": 100,
"started_at": 1692771667037,
"created_at": 1692771650657,
"finished_at": 1692771669037,
"status": "SUCCEEDED",
"texture_urls": [
{
"base_color": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/texture_0.png?Expires=***"
}
],
"preceding_tasks": 0,
"task_error": {
"message": ""
}
}
]

Copy
Copied!
GET
/openapi/v2/text-to-3d/:id/stream
Stream a Text to 3D Task
This endpoint streams real-time updates for a Text to 3D task using Server-Sent Events (SSE).

Parameters
Name
id
Type
path
Description
Unique identifier for the Text to 3D task to stream.

Returns
Returns a stream of The Text to 3D Task Objects as Server-Sent Events.

For PENDING or IN_PROGRESS tasks, the response stream will only include necessary progress and status fields.

Request
cURL
JavaScript
Python
GET
/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578/stream
curl -N [https://api.meshy.ai/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578/stream](https://api.meshy.ai/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578/stream "https://api.meshy.ai/openapi/v2/text-to-3d/018a210d-8ba4-705c-b111-1f1776f7f578/stream")
-H "Authorization: Bearer ${YOUR_API_KEY}"

Copy
Copied!
Response Stream
// Error event example
event: error
data: {
"status_code": 404,
"message": "Task not found"
}

// Message event examples illustrate task progress.
// For PENDING or IN_PROGRESS tasks, the response stream will not include all fields.
event: message
data: {
"id": "018a210d-8ba4-705c-b111-1f1776f7f578",
"progress": 0,
"status": "PENDING"
}

event: message
data: {
"id": "018a210d-8ba4-705c-b111-1f1776f7f578",
"progress": 50,
"status": "IN_PROGRESS"
}

event: message
data: {
"id": "018a210d-8ba4-705c-b111-1f1776f7f578",
"type": "text-to-3d-preview",
"progress": 100,
"status": "SUCCEEDED",
"created_at": 1692771650657,
"started_at": 1692771667037,
"finished_at": 1692771669037,
"model_urls": {
"glb": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.glb?Expires=***"
},
"texture_urls": [
{
"base_color": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/texture_0.png?Expires=***",
"metallic": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/texture_0_metallic.png?Expires=XXX",
"normal": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/texture_0_roughness.png?Expires=XXX",
"roughness": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/texture_0_normal.png?Expires=XXX"
}
],
"preceding_tasks": 0,
"task_error": {
"message": ""
}
}

Copy
Copied!
The Text to 3D Task Object
The Text to 3D Task object is a work unit that Meshy keeps track of to generate a 3D model from a text input. There are two stages of the Text to 3D API, preview and refine. Preview stage is for generating a mesh-only 3D model, and refine stage is for generating a textured 3D model based on the preview stage's result.

The object has the following properties:

Properties
Name
id
Type
string
Description
Unique identifier for the task. While we use a k-sortable UUID for task ids as the implementation detail, you should not make any assumptions about the format of the id.

Name
type
Type
string
Description
Type of the Text to 3D task. Possible values are text-to-3d-preview for preview stage tasks and text-to-3d-refine for refine stage tasks.

Name
model_urls
Type
object
Description
Downloadable URL to the textured 3D model file generated by Meshy. The property for a format will be omitted if the format is not generated instead of returning an empty string.

Name
glb
Type
string
Description
Downloadable URL to the GLB file.

Name
fbx
Type
string
Description
Downloadable URL to the FBX file.

Name
usdz
Type
string
Description
Downloadable URL to the USDZ file.

Name
obj
Type
string
Description
Downloadable URL to the OBJ file.

Name
mtl
Type
string
Description
Downloadable URL to the MTL file.

Name
stl
Type
string
Description
Downloadable URL to the STL file.

Name
3mf
Type
string
Description
Downloadable URL to the 3MF file. Only present when 3mf was requested via target_formats.

Name
prompt
Type
string
Description
This is unmodified prompt that was used to create the task.

Name
negative_prompt
Type
string
Description
Deprecated field maintained for backward compatibility. This field has no functional impact on generated models.

Name
art_style
Type
string
Description
Deprecated field maintained for backward compatibility. This is the unmodified art_style that was used to create the preview task. The art_style parameter is deprecated and not supported by Meshy-6.

Name
texture_richness
Type
string
Description
Deprecated field maintained for backward compatibility. This field has no functional impact on generated models.

Name
texture_prompt
Type
string
Description
Additional text prompt provided to guide the texturing process during the refine stage.

Name
texture_image_url
Type
string
Description
Downloadable URL to the texture image that was used to guide the texturing process.

Name
thumbnail_url
Type
string
Description
Downloadable URL to the thumbnail image of the model file.

Name
video_url
Type
string
Description
Deprecated field returning the downloadable URL to the preview video. This field will be removed in a future release; avoid relying on it.

Name
progress
Type
integer
Description
Progress of the task. If the task is not started yet, this property will be 0. Once the task has succeeded, this will become 100.

Name
started_at
Type
timestamp
Description
Timestamp of when the task was started, in milliseconds. If the task is not started yet, this property will be 0.

A timestamp represents the number of milliseconds elapsed since January 1, 1970 UTC, following the RFC 3339 standard. For example, Friday, September 1, 2023 12:00:00 PM GMT is represented as 1693569600000. This applies to all timestamps in Meshy API.

Name
created_at
Type
timestamp
Description
Timestamp of when the task was created, in milliseconds.

Name
finished_at
Type
timestamp
Description
Timestamp of when the task was finished, in milliseconds. If the task is not finished yet, this property will be 0.

Name
status
Type
string
Description
Status of the task. Possible values are one of PENDING, IN_PROGRESS, SUCCEEDED, FAILED, CANCELED.

Name
texture_urls
Type
array
Description
An array of texture URL objects that are generated from the task. Normally this only contains one texture URL object. Each texture URL has the following properties:

Name
base_color
Type
string
Description
Downloadable URL to the base color map image.

Name
metallic
Type
string
Description
Downloadable URL to the metallic map image.

If the task is created with enable_pbr: false, this property will be omitted.

Name
normal
Type
string
Description
Downloadable URL to the normal map image.

If the task is created with enable_pbr: false, this property will be omitted.

Name
roughness
Type
string
Description
Downloadable URL to the roughness map image.

If the task is created with enable_pbr: false, this property will be omitted.

Name
preceding_tasks
Type
integer
Description
The count of preceding tasks.

The value of this field is meaningful only if the task status is PENDING.

Name
task_error
Type
object
Description
Error object that contains the error message if the task failed. The message property should be empty if the task succeeded.

Name
message
Type
string
Description
Detailed error message.

Example Text to 3D Task Object
{
"id": "018a210d-8ba4-705c-b111-1f1776f7f578",
"type": "text-to-3d-preview",
"model_urls": {
"glb": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.glb?Expires=***",
"fbx": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.fbx?Expires=***",
"usdz": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.usdz?Expires=***",
"obj": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.obj?Expires=***",
"mtl": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.mtl?Expires=***",
"stl": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/model.stl?Expires=***"
},
"prompt": "a monster mask",
"art_style": "realistic",
"texture_richness": "high",
"texture_prompt": "green slimy skin with scales and warts",
"texture_image_url": "",
"thumbnail_url": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/preview.png?Expires=***",
"video_url": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/preview.mp4?Expires=***",
"progress": 100,
"seed": 1234,
"started_at": 1692771667037,
"created_at": 1692771650657,
"finished_at": 1692771669037,
"status": "SUCCEEDED",
"texture_urls": [
{
"base_color": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/texture_0.png?Expires=***",
"metallic": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/texture_0_metallic.png?Expires=XXX",
"normal": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/texture_0_roughness.png?Expires=XXX",
"roughness": "[https://assets.meshy.ai/](https://assets.meshy.ai/ "https://assets.meshy.ai/")***/tasks/018a210d-8ba4-705c-b111-1f1776f7f578/output/texture_0_normal.png?Expires=XXX"
}
],
"preceding_tasks": 0,
"task_error": {
"message": ""
}
}
