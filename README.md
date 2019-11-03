# AnyRestApi2Mqtt
This Docker image allows you to connect any REST API to MQTT by simply defining the API in a YAML file.

## How to use
Run the docker image as followed
```bash
docker run -d --name anyrestapi2mqtt -v config.yml:/app/config.yml bluewalk/anyrestapi2mqtt
```

## Creating configuration
The YAML file has the following buildup
```yaml
mqtt:
  host: 127.0.0.1
  port: 1883
  username:
  password:

apis:
  ...
```

API definitions contain at least
```yaml
  - name: myapi
    base-url: https://domain.com/api/v1
    base-topic: myapiv1
    authentication:
      ...
    headers:
      Content-Type: application/json
    endpoints:
      ...
    on-start:
      - endpont1
```

* `base-topic` defines the start of the MQTT topic where endpoint topics are appended to.

* `headers` are there to define global headers, eg `Content-type: application/json`. These headers are sent with every request (including requests for authentication). This can also be used as an alternative for any authentication scheme if you have an API key that doesn't change (quite unsafe `;-)`)

* `on-start` lists endpoints that are automatically executed upon starting. This can be used to automatically query an endpoint and publish it's contents upon starting.

---

## Authentication
There are four available authentication types supported: `none`, `bearer`, `header` and `basic`.

|Property|Available values|
|--|--|
|`path`|string: Path of the endpoint|
|`type`|`none`, `bearer`, `header`, `basic`|
|`body`|Key-value pairs, eg `username: user@domain.com`|
|`body-encoding`|`json`, `xml`, `yaml`|
|`method`|`get`, `post`, `put`. `delete`, `patch`|
|`headers`|Key-value pairs, eg. `X-App: MyApp`|
|`token-path`|string: selector for defined `body-encoding`|

### - None
```yaml
      type: none
```

### - Bearer
Using `body-encoding` the contents of `body` will be sent using the method defined by `method`.

From the response, the value defined by `token-path` will be used and saved as the access token. This token will be extracted and therefore it's required to be defined as an selector for the specific `body-encoding`, e.g. `acccess_token` will fetch the value `access_token` from a JSON body like `{"access_token": "aaa...", "expires": 12399348}` (JSON-Path).

```yaml
      path: auth/login
      method: POST
      type: bearer
      headers:
        ...
      body:
        email: user@domain.com
        password: mypassword01!
      body-encoding: json
      token-path: access_token
```

### - Header
Same as the Bearer authentication type an endpoint will be contacted and by the `token-path` the token will be saved and added as the value of the header defined by `header-name`
```yaml
      path: auth/login
      method: POST
      type: header
      headers:
        ...
      body:
        email: user@domain.com
        password: mypassword01!
      header-name: X-Auth-Token
      body-encoding: json
      token-path: access_token
```

### - Basic
Simply send basic authentication credentials to the API endpoint
```yaml
    type: basic
    basic-username: user@domain.com
    basic-password: mypassword01!
```

---

## Endpoint
Endpoints are defined by first specifying the name of the endpoint and then the contents. There are two types of MQTT actions (`publish` and `subscribe`).

* MQTT topics defined with `publish` will execute the call and the response is searched by the defined `selector` (see `token-path` at authentication). Items matching the selector will be published to the MQTT topic defined.

* MQTT topics defined with `subscribe` will execute the call when a message is received at the MQTT topic defined. The body of that message will be sent as the body of the endpoint.

* MQTT topics can contain variables that will be placed in the `path` of the endpoint when matching, see example below.

|Property|Available values|
|--|--|
|`path`|string: Path of the endpoint|
|`method`|`get`, `post`, `put`. `delete`, `patch`|
|`encoding`|`json`, `xml`, `yaml`|
|`headers`|Key-value pairs, eg. `X-App: MyApp`|
|`selector`|string: selector for defined `body-encoding`|
|`mqtt`|properties `topic`, `action`|
|`mqtt`:`topic`|string: MQTT topci|
|`mqtt`:`action`|`publish` or `subscribe`

* Get request that will publish all items of the `data` property of the JSON response to the topic `myapiv1/items`
```yaml
      get-items:
        path: items
        method: GET
        encoding: json
        headers:
          Accept: application/json
        selector: data[*]
        mqtt:
          topic: items
          action: publish
```

* Post request that will execute a call to `items/%id%/value` when a message is received at `myapiv1/%id%/value` with the body of the MQTT message.

  For example the topic `myapiv1/1234/value` will call the API endpint `items/1234/value`.
```yaml
      set-item-value:
        path: items/%id%/value
        method: POST
        headers:
          Accept: application/json
        mqtt:
          topic: "%id%/value"
          action: subscribe
```

* Delete request that will execute a call to `items/%id%` when a message is received at `myapiv1/%id%/delete`
```yaml
      delete-item:
        path: items/%id%
        method: DELETE
        mqtt:
          topic: "%id%/delete"
          action: subscribe
```