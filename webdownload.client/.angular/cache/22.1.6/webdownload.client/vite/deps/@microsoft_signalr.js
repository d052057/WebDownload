import { n as _objectSpread2, t as _asyncToGenerator } from "./asyncToGenerator-BJgYDK9H.js";
//#region \0rolldown/runtime.js
var __require = /* @__PURE__ */ ((x) => typeof require !== "undefined" ? require : typeof Proxy !== "undefined" ? new Proxy(x, { get: (a, b) => (typeof require !== "undefined" ? require : a)[b] }) : x)(function(x) {
	if (typeof require !== "undefined") return require.apply(this, arguments);
	throw Error("Calling `require` for \"" + x + "\" in an environment that doesn't expose the `require` function. See https://rolldown.rs/in-depth/bundling-cjs#require-external-modules for more details.");
});
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/Errors.js
/** Error thrown when an HTTP request fails. */
var HttpError = class extends Error {
	/** Constructs a new instance of {@link @microsoft/signalr.HttpError}.
	*
	* @param {string} errorMessage A descriptive error message.
	* @param {number} statusCode The HTTP status code represented by this error.
	*/
	constructor(errorMessage, statusCode) {
		const trueProto = new.target.prototype;
		super(`${errorMessage}: Status code '${statusCode}'`);
		this.statusCode = statusCode;
		this.__proto__ = trueProto;
	}
};
/** Error thrown when a timeout elapses. */
var TimeoutError = class extends Error {
	/** Constructs a new instance of {@link @microsoft/signalr.TimeoutError}.
	*
	* @param {string} errorMessage A descriptive error message.
	*/
	constructor(errorMessage = "A timeout occurred.") {
		const trueProto = new.target.prototype;
		super(errorMessage);
		this.__proto__ = trueProto;
	}
};
/** Error thrown when an action is aborted. */
var AbortError = class extends Error {
	/** Constructs a new instance of {@link AbortError}.
	*
	* @param {string} errorMessage A descriptive error message.
	*/
	constructor(errorMessage = "An abort occurred.") {
		const trueProto = new.target.prototype;
		super(errorMessage);
		this.__proto__ = trueProto;
	}
};
/** Error thrown when the selected transport is unsupported by the browser. */
/** @private */
var UnsupportedTransportError = class extends Error {
	/** Constructs a new instance of {@link @microsoft/signalr.UnsupportedTransportError}.
	*
	* @param {string} message A descriptive error message.
	* @param {HttpTransportType} transport The {@link @microsoft/signalr.HttpTransportType} this error occurred on.
	*/
	constructor(message, transport) {
		const trueProto = new.target.prototype;
		super(message);
		this.transport = transport;
		this.errorType = "UnsupportedTransportError";
		this.__proto__ = trueProto;
	}
};
/** Error thrown when the selected transport is disabled by the browser. */
/** @private */
var DisabledTransportError = class extends Error {
	/** Constructs a new instance of {@link @microsoft/signalr.DisabledTransportError}.
	*
	* @param {string} message A descriptive error message.
	* @param {HttpTransportType} transport The {@link @microsoft/signalr.HttpTransportType} this error occurred on.
	*/
	constructor(message, transport) {
		const trueProto = new.target.prototype;
		super(message);
		this.transport = transport;
		this.errorType = "DisabledTransportError";
		this.__proto__ = trueProto;
	}
};
/** Error thrown when the selected transport cannot be started. */
/** @private */
var FailedToStartTransportError = class extends Error {
	/** Constructs a new instance of {@link @microsoft/signalr.FailedToStartTransportError}.
	*
	* @param {string} message A descriptive error message.
	* @param {HttpTransportType} transport The {@link @microsoft/signalr.HttpTransportType} this error occurred on.
	*/
	constructor(message, transport) {
		const trueProto = new.target.prototype;
		super(message);
		this.transport = transport;
		this.errorType = "FailedToStartTransportError";
		this.__proto__ = trueProto;
	}
};
/** Error thrown when the negotiation with the server failed to complete. */
/** @private */
var FailedToNegotiateWithServerError = class extends Error {
	/** Constructs a new instance of {@link @microsoft/signalr.FailedToNegotiateWithServerError}.
	*
	* @param {string} message A descriptive error message.
	*/
	constructor(message) {
		const trueProto = new.target.prototype;
		super(message);
		this.errorType = "FailedToNegotiateWithServerError";
		this.__proto__ = trueProto;
	}
};
/** Error thrown when multiple errors have occurred. */
/** @private */
var AggregateErrors = class extends Error {
	/** Constructs a new instance of {@link @microsoft/signalr.AggregateErrors}.
	*
	* @param {string} message A descriptive error message.
	* @param {Error[]} innerErrors The collection of errors this error is aggregating.
	*/
	constructor(message, innerErrors) {
		const trueProto = new.target.prototype;
		super(message);
		this.innerErrors = innerErrors;
		this.__proto__ = trueProto;
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/HttpClient.js
/** Represents an HTTP response. */
var HttpResponse = class {
	constructor(statusCode, statusText, content) {
		this.statusCode = statusCode;
		this.statusText = statusText;
		this.content = content;
	}
};
/** Abstraction over an HTTP client.
*
* This class provides an abstraction over an HTTP client so that a different implementation can be provided on different platforms.
*/
var HttpClient = class {
	get(url, options) {
		return this.send(_objectSpread2(_objectSpread2({}, options), {}, {
			method: "GET",
			url
		}));
	}
	post(url, options) {
		return this.send(_objectSpread2(_objectSpread2({}, options), {}, {
			method: "POST",
			url
		}));
	}
	delete(url, options) {
		return this.send(_objectSpread2(_objectSpread2({}, options), {}, {
			method: "DELETE",
			url
		}));
	}
	/** Gets all cookies that apply to the specified URL.
	*
	* @param url The URL that the cookies are valid for.
	* @returns {string} A string containing all the key-value cookie pairs for the specified URL.
	*/
	getCookieString(url) {
		return "";
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/ILogger.js
/** Indicates the severity of a log message.
*
* Log Levels are ordered in increasing severity. So `Debug` is more severe than `Trace`, etc.
*/
var LogLevel;
(function(LogLevel) {
	/** Log level for very low severity diagnostic messages. */
	LogLevel[LogLevel["Trace"] = 0] = "Trace";
	/** Log level for low severity diagnostic messages. */
	LogLevel[LogLevel["Debug"] = 1] = "Debug";
	/** Log level for informational diagnostic messages. */
	LogLevel[LogLevel["Information"] = 2] = "Information";
	/** Log level for diagnostic messages that indicate a non-fatal problem. */
	LogLevel[LogLevel["Warning"] = 3] = "Warning";
	/** Log level for diagnostic messages that indicate a failure in the current operation. */
	LogLevel[LogLevel["Error"] = 4] = "Error";
	/** Log level for diagnostic messages that indicate a failure that will terminate the entire application. */
	LogLevel[LogLevel["Critical"] = 5] = "Critical";
	/** The highest possible log level. Used when configuring logging to indicate that no log messages should be emitted. */
	LogLevel[LogLevel["None"] = 6] = "None";
})(LogLevel || (LogLevel = {}));
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/Loggers.js
/** A logger that does nothing when log messages are sent to it. */
var NullLogger = class {
	constructor() {}
	/** @inheritDoc */
	log(_logLevel, _message) {}
};
/** The singleton instance of the {@link @microsoft/signalr.NullLogger}. */
NullLogger.instance = new NullLogger();
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/pkg-version.js
var VERSION = "10.0.11";
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/Utils.js
/** @private */
var Arg = class {
	static isRequired(val, name) {
		if (val === null || val === void 0) throw new Error(`The '${name}' argument is required.`);
	}
	static isNotEmpty(val, name) {
		if (!val || val.match(/^\s*$/)) throw new Error(`The '${name}' argument should not be empty.`);
	}
	static isIn(val, values, name) {
		if (!(val in values)) throw new Error(`Unknown ${name} value: ${val}.`);
	}
};
/** @private */
var Platform = class Platform {
	static get isBrowser() {
		return !Platform.isNode && typeof window === "object" && typeof window.document === "object";
	}
	static get isWebWorker() {
		return !Platform.isNode && typeof self === "object" && "importScripts" in self;
	}
	static get isReactNative() {
		return !Platform.isNode && typeof window === "object" && typeof window.document === "undefined";
	}
	static get isNode() {
		return typeof process !== "undefined" && process.release && process.release.name === "node";
	}
};
/** @private */
function getDataDetail(data, includeContent) {
	let detail = "";
	if (isArrayBuffer(data)) {
		detail = `Binary data of length ${data.byteLength}`;
		if (includeContent) detail += `. Content: '${formatArrayBuffer(data)}'`;
	} else if (typeof data === "string") {
		detail = `String data of length ${data.length}`;
		if (includeContent) detail += `. Content: '${data}'`;
	}
	return detail;
}
/** @private */
function formatArrayBuffer(data) {
	const view = new Uint8Array(data);
	let str = "";
	view.forEach((num) => {
		str += `0x${num < 16 ? "0" : ""}${num.toString(16)} `;
	});
	return str.substring(0, str.length - 1);
}
/** @private */
function isArrayBuffer(val) {
	return val && typeof ArrayBuffer !== "undefined" && (val instanceof ArrayBuffer || val.constructor && val.constructor.name === "ArrayBuffer");
}
/** @private */
function sendMessage(_x, _x2, _x3, _x4, _x5, _x6) {
	return _sendMessage.apply(this, arguments);
}
function _sendMessage() {
	_sendMessage = _asyncToGenerator(function* (logger, transportName, httpClient, url, content, options) {
		const headers = {};
		const [name, value] = getUserAgentHeader();
		headers[name] = value;
		logger.log(LogLevel.Trace, `(${transportName} transport) sending data. ${getDataDetail(content, options.logMessageContent)}.`);
		const responseType = isArrayBuffer(content) ? "arraybuffer" : "text";
		const response = yield httpClient.post(url, {
			content,
			headers: _objectSpread2(_objectSpread2({}, headers), options.headers),
			responseType,
			timeout: options.timeout,
			withCredentials: options.withCredentials
		});
		logger.log(LogLevel.Trace, `(${transportName} transport) request complete. Response status: ${response.statusCode}.`);
	});
	return _sendMessage.apply(this, arguments);
}
/** @private */
function createLogger(logger) {
	if (logger === void 0) return new ConsoleLogger(LogLevel.Information);
	if (logger === null) return NullLogger.instance;
	if (logger.log !== void 0) return logger;
	return new ConsoleLogger(logger);
}
/** @private */
var SubjectSubscription = class {
	constructor(subject, observer) {
		this._subject = subject;
		this._observer = observer;
	}
	dispose() {
		const index = this._subject.observers.indexOf(this._observer);
		if (index > -1) this._subject.observers.splice(index, 1);
		if (this._subject.observers.length === 0 && this._subject.cancelCallback) this._subject.cancelCallback().catch((_) => {});
	}
};
/** @private */
var ConsoleLogger = class {
	constructor(minimumLogLevel) {
		this._minLevel = minimumLogLevel;
		this.out = console;
	}
	log(logLevel, message) {
		if (logLevel >= this._minLevel) {
			const msg = `[${(/* @__PURE__ */ new Date()).toISOString()}] ${LogLevel[logLevel]}: ${message}`;
			switch (logLevel) {
				case LogLevel.Critical:
				case LogLevel.Error:
					this.out.error(msg);
					break;
				case LogLevel.Warning:
					this.out.warn(msg);
					break;
				case LogLevel.Information:
					this.out.info(msg);
					break;
				default:
					this.out.log(msg);
					break;
			}
		}
	}
};
/** @private */
function getUserAgentHeader() {
	let userAgentHeaderName = "X-SignalR-User-Agent";
	if (Platform.isNode) userAgentHeaderName = "User-Agent";
	return [userAgentHeaderName, constructUserAgent(VERSION, getOsName(), getRuntime(), getRuntimeVersion())];
}
/** @private */
function constructUserAgent(version, os, runtime, runtimeVersion) {
	let userAgent = "Microsoft SignalR/";
	const majorAndMinor = version.split(".");
	userAgent += `${majorAndMinor[0]}.${majorAndMinor[1]}`;
	userAgent += ` (${version}; `;
	if (os && os !== "") userAgent += `${os}; `;
	else userAgent += "Unknown OS; ";
	userAgent += `${runtime}`;
	if (runtimeVersion) userAgent += `; ${runtimeVersion}`;
	else userAgent += "; Unknown Runtime Version";
	userAgent += ")";
	return userAgent;
}
/*#__PURE__*/ function getOsName() {
	if (Platform.isNode) switch (process.platform) {
		case "win32": return "Windows NT";
		case "darwin": return "macOS";
		case "linux": return "Linux";
		default: return process.platform;
	}
	else return "";
}
/*#__PURE__*/ function getRuntimeVersion() {
	if (Platform.isNode) return process.versions.node;
}
function getRuntime() {
	if (Platform.isNode) return "NodeJS";
	else return "Browser";
}
/** @private */
function getErrorString(e) {
	if (e.stack) return e.stack;
	else if (e.message) return e.message;
	return `${e}`;
}
/** @private */
function getGlobalThis() {
	if (typeof globalThis !== "undefined") return globalThis;
	if (typeof self !== "undefined") return self;
	if (typeof window !== "undefined") return window;
	if (typeof global !== "undefined") return global;
	throw new Error("could not find global");
}
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/FetchHttpClient.js
var FetchHttpClient = class extends HttpClient {
	constructor(logger) {
		super();
		this._logger = logger;
		if (typeof fetch === "undefined" || Platform.isNode) {
			const requireFunc = typeof __webpack_require__ === "function" ? __non_webpack_require__ : __require;
			this._jar = new (requireFunc("tough-cookie")).CookieJar();
			if (typeof fetch === "undefined") this._fetchType = requireFunc("node-fetch");
			else this._fetchType = fetch;
			this._fetchType = requireFunc("fetch-cookie")(this._fetchType, this._jar);
		} else this._fetchType = fetch.bind(getGlobalThis());
		if (typeof AbortController === "undefined") {
			const requireFunc = typeof __webpack_require__ === "function" ? __non_webpack_require__ : __require;
			this._abortControllerType = requireFunc("abort-controller");
		} else this._abortControllerType = AbortController;
	}
	/** @inheritDoc */
	send(request) {
		var _this = this;
		return _asyncToGenerator(function* () {
			if (request.abortSignal && request.abortSignal.aborted) throw new AbortError();
			if (!request.method) throw new Error("No method defined.");
			if (!request.url) throw new Error("No url defined.");
			const abortController = new _this._abortControllerType();
			let error;
			if (request.abortSignal) request.abortSignal.onabort = () => {
				abortController.abort();
				error = new AbortError();
			};
			let timeoutId = null;
			if (request.timeout) {
				const msTimeout = request.timeout;
				timeoutId = setTimeout(() => {
					abortController.abort();
					_this._logger.log(LogLevel.Warning, `Timeout from HTTP request.`);
					error = new TimeoutError();
				}, msTimeout);
			}
			if (request.content === "") request.content = void 0;
			if (request.content) {
				request.headers = request.headers || {};
				if (isArrayBuffer(request.content)) request.headers["Content-Type"] = "application/octet-stream";
				else request.headers["Content-Type"] = "text/plain;charset=UTF-8";
			}
			let response;
			try {
				response = yield _this._fetchType(request.url, {
					body: request.content,
					cache: "no-cache",
					credentials: request.withCredentials === true ? "include" : "same-origin",
					headers: _objectSpread2({ "X-Requested-With": "XMLHttpRequest" }, request.headers),
					method: request.method,
					mode: "cors",
					redirect: "follow",
					signal: abortController.signal
				});
			} catch (e) {
				if (error) throw error;
				_this._logger.log(LogLevel.Warning, `Error from HTTP request. ${e}.`);
				throw e;
			} finally {
				if (timeoutId) clearTimeout(timeoutId);
				if (request.abortSignal) request.abortSignal.onabort = null;
			}
			if (!response.ok) throw new HttpError((yield deserializeContent(response, "text")) || response.statusText, response.status);
			const payload = yield deserializeContent(response, request.responseType);
			return new HttpResponse(response.status, response.statusText, payload);
		})();
	}
	getCookieString(url) {
		let cookies = "";
		if (Platform.isNode && this._jar) this._jar.getCookies(url, (e, c) => cookies = c.join("; "));
		return cookies;
	}
};
function deserializeContent(response, responseType) {
	let content;
	switch (responseType) {
		case "arraybuffer":
			content = response.arrayBuffer();
			break;
		case "text":
			content = response.text();
			break;
		case "blob":
		case "document":
		case "json": throw new Error(`${responseType} is not supported.`);
		default:
			content = response.text();
			break;
	}
	return content;
}
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/XhrHttpClient.js
var XhrHttpClient = class extends HttpClient {
	constructor(logger) {
		super();
		this._logger = logger;
	}
	/** @inheritDoc */
	send(request) {
		if (request.abortSignal && request.abortSignal.aborted) return Promise.reject(new AbortError());
		if (!request.method) return Promise.reject(/* @__PURE__ */ new Error("No method defined."));
		if (!request.url) return Promise.reject(/* @__PURE__ */ new Error("No url defined."));
		return new Promise((resolve, reject) => {
			const xhr = new XMLHttpRequest();
			xhr.open(request.method, request.url, true);
			xhr.withCredentials = request.withCredentials === void 0 ? true : request.withCredentials;
			xhr.setRequestHeader("X-Requested-With", "XMLHttpRequest");
			if (request.content === "") request.content = void 0;
			if (request.content) if (isArrayBuffer(request.content)) xhr.setRequestHeader("Content-Type", "application/octet-stream");
			else xhr.setRequestHeader("Content-Type", "text/plain;charset=UTF-8");
			const headers = request.headers;
			if (headers) Object.keys(headers).forEach((header) => {
				xhr.setRequestHeader(header, headers[header]);
			});
			if (request.responseType) xhr.responseType = request.responseType;
			if (request.abortSignal) request.abortSignal.onabort = () => {
				xhr.abort();
				reject(new AbortError());
			};
			if (request.timeout) xhr.timeout = request.timeout;
			xhr.onload = () => {
				if (request.abortSignal) request.abortSignal.onabort = null;
				if (xhr.status >= 200 && xhr.status < 300) resolve(new HttpResponse(xhr.status, xhr.statusText, xhr.response || xhr.responseText));
				else reject(new HttpError(xhr.response || xhr.responseText || xhr.statusText, xhr.status));
			};
			xhr.onerror = () => {
				this._logger.log(LogLevel.Warning, `Error from HTTP request. ${xhr.status}: ${xhr.statusText}.`);
				reject(new HttpError(xhr.statusText, xhr.status));
			};
			xhr.ontimeout = () => {
				this._logger.log(LogLevel.Warning, `Timeout from HTTP request.`);
				reject(new TimeoutError());
			};
			xhr.send(request.content);
		});
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/DefaultHttpClient.js
/** Default implementation of {@link @microsoft/signalr.HttpClient}. */
var DefaultHttpClient = class extends HttpClient {
	/** Creates a new instance of the {@link @microsoft/signalr.DefaultHttpClient}, using the provided {@link @microsoft/signalr.ILogger} to log messages. */
	constructor(logger) {
		super();
		if (typeof fetch !== "undefined" || Platform.isNode) this._httpClient = new FetchHttpClient(logger);
		else if (typeof XMLHttpRequest !== "undefined") this._httpClient = new XhrHttpClient(logger);
		else throw new Error("No usable HttpClient found.");
	}
	/** @inheritDoc */
	send(request) {
		if (request.abortSignal && request.abortSignal.aborted) return Promise.reject(new AbortError());
		if (!request.method) return Promise.reject(/* @__PURE__ */ new Error("No method defined."));
		if (!request.url) return Promise.reject(/* @__PURE__ */ new Error("No url defined."));
		return this._httpClient.send(request);
	}
	getCookieString(url) {
		return this._httpClient.getCookieString(url);
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/TextMessageFormat.js
/** @private */
var TextMessageFormat = class TextMessageFormat {
	static write(output) {
		return `${output}${TextMessageFormat.RecordSeparator}`;
	}
	static parse(input) {
		if (input[input.length - 1] !== TextMessageFormat.RecordSeparator) throw new Error("Message is incomplete.");
		const messages = input.split(TextMessageFormat.RecordSeparator);
		messages.pop();
		return messages;
	}
};
TextMessageFormat.RecordSeparatorCode = 30;
TextMessageFormat.RecordSeparator = String.fromCharCode(TextMessageFormat.RecordSeparatorCode);
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/HandshakeProtocol.js
/** @private */
var HandshakeProtocol = class {
	writeHandshakeRequest(handshakeRequest) {
		return TextMessageFormat.write(JSON.stringify(handshakeRequest));
	}
	parseHandshakeResponse(data) {
		let messageData;
		let remainingData;
		if (isArrayBuffer(data)) {
			const binaryData = new Uint8Array(data);
			const separatorIndex = binaryData.indexOf(TextMessageFormat.RecordSeparatorCode);
			if (separatorIndex === -1) throw new Error("Message is incomplete.");
			const responseLength = separatorIndex + 1;
			messageData = String.fromCharCode.apply(null, Array.prototype.slice.call(binaryData.slice(0, responseLength)));
			remainingData = binaryData.byteLength > responseLength ? binaryData.slice(responseLength).buffer : null;
		} else {
			const textData = data;
			const separatorIndex = textData.indexOf(TextMessageFormat.RecordSeparator);
			if (separatorIndex === -1) throw new Error("Message is incomplete.");
			const responseLength = separatorIndex + 1;
			messageData = textData.substring(0, responseLength);
			remainingData = textData.length > responseLength ? textData.substring(responseLength) : null;
		}
		const messages = TextMessageFormat.parse(messageData);
		const response = JSON.parse(messages[0]);
		if (response.type) throw new Error("Expected a handshake response from the server.");
		return [remainingData, response];
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/IHubProtocol.js
/** Defines the type of a Hub Message. */
var MessageType;
(function(MessageType) {
	/** Indicates the message is an Invocation message and implements the {@link @microsoft/signalr.InvocationMessage} interface. */
	MessageType[MessageType["Invocation"] = 1] = "Invocation";
	/** Indicates the message is a StreamItem message and implements the {@link @microsoft/signalr.StreamItemMessage} interface. */
	MessageType[MessageType["StreamItem"] = 2] = "StreamItem";
	/** Indicates the message is a Completion message and implements the {@link @microsoft/signalr.CompletionMessage} interface. */
	MessageType[MessageType["Completion"] = 3] = "Completion";
	/** Indicates the message is a Stream Invocation message and implements the {@link @microsoft/signalr.StreamInvocationMessage} interface. */
	MessageType[MessageType["StreamInvocation"] = 4] = "StreamInvocation";
	/** Indicates the message is a Cancel Invocation message and implements the {@link @microsoft/signalr.CancelInvocationMessage} interface. */
	MessageType[MessageType["CancelInvocation"] = 5] = "CancelInvocation";
	/** Indicates the message is a Ping message and implements the {@link @microsoft/signalr.PingMessage} interface. */
	MessageType[MessageType["Ping"] = 6] = "Ping";
	/** Indicates the message is a Close message and implements the {@link @microsoft/signalr.CloseMessage} interface. */
	MessageType[MessageType["Close"] = 7] = "Close";
	MessageType[MessageType["Ack"] = 8] = "Ack";
	MessageType[MessageType["Sequence"] = 9] = "Sequence";
})(MessageType || (MessageType = {}));
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/Subject.js
/** Stream implementation to stream items to the server. */
var Subject = class {
	constructor() {
		this.observers = [];
	}
	next(item) {
		for (const observer of this.observers) observer.next(item);
	}
	error(err) {
		for (const observer of this.observers) if (observer.error) observer.error(err);
	}
	complete() {
		for (const observer of this.observers) if (observer.complete) observer.complete();
	}
	subscribe(observer) {
		this.observers.push(observer);
		return new SubjectSubscription(this, observer);
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/MessageBuffer.js
/** @private */
var MessageBuffer = class {
	constructor(protocol, connection, bufferSize) {
		this._bufferSize = 1e5;
		this._messages = [];
		this._totalMessageCount = 0;
		this._waitForSequenceMessage = false;
		this._nextReceivingSequenceId = 1;
		this._latestReceivedSequenceId = 0;
		this._bufferedByteCount = 0;
		this._reconnectInProgress = false;
		this._protocol = protocol;
		this._connection = connection;
		this._bufferSize = bufferSize;
	}
	_send(message) {
		var _this = this;
		return _asyncToGenerator(function* () {
			const serializedMessage = _this._protocol.writeMessage(message);
			let backpressurePromise = Promise.resolve();
			if (_this._isInvocationMessage(message)) {
				_this._totalMessageCount++;
				let backpressurePromiseResolver = () => {};
				let backpressurePromiseRejector = () => {};
				if (isArrayBuffer(serializedMessage)) _this._bufferedByteCount += serializedMessage.byteLength;
				else _this._bufferedByteCount += serializedMessage.length;
				if (_this._bufferedByteCount >= _this._bufferSize) backpressurePromise = new Promise((resolve, reject) => {
					backpressurePromiseResolver = resolve;
					backpressurePromiseRejector = reject;
				});
				_this._messages.push(new BufferedItem(serializedMessage, _this._totalMessageCount, backpressurePromiseResolver, backpressurePromiseRejector));
			}
			try {
				if (!_this._reconnectInProgress) yield _this._connection.send(serializedMessage);
			} catch (_unused) {
				_this._disconnected();
			}
			yield backpressurePromise;
		})();
	}
	_ack(ackMessage) {
		let newestAckedMessage = -1;
		for (let index = 0; index < this._messages.length; index++) {
			const element = this._messages[index];
			if (element._id <= ackMessage.sequenceId) {
				newestAckedMessage = index;
				if (isArrayBuffer(element._message)) this._bufferedByteCount -= element._message.byteLength;
				else this._bufferedByteCount -= element._message.length;
				element._resolver();
			} else if (this._bufferedByteCount < this._bufferSize) element._resolver();
			else break;
		}
		if (newestAckedMessage !== -1) this._messages = this._messages.slice(newestAckedMessage + 1);
	}
	_shouldProcessMessage(message) {
		if (this._waitForSequenceMessage) if (message.type !== MessageType.Sequence) return false;
		else {
			this._waitForSequenceMessage = false;
			return true;
		}
		if (!this._isInvocationMessage(message)) return true;
		const currentId = this._nextReceivingSequenceId;
		this._nextReceivingSequenceId++;
		if (currentId <= this._latestReceivedSequenceId) {
			if (currentId === this._latestReceivedSequenceId) this._ackTimer();
			return false;
		}
		this._latestReceivedSequenceId = currentId;
		this._ackTimer();
		return true;
	}
	_resetSequence(message) {
		if (message.sequenceId > this._nextReceivingSequenceId) {
			this._connection.stop(/* @__PURE__ */ new Error("Sequence ID greater than amount of messages we've received."));
			return;
		}
		this._nextReceivingSequenceId = message.sequenceId;
	}
	_disconnected() {
		this._reconnectInProgress = true;
		this._waitForSequenceMessage = true;
	}
	_resend() {
		var _this2 = this;
		return _asyncToGenerator(function* () {
			const sequenceId = _this2._messages.length !== 0 ? _this2._messages[0]._id : _this2._totalMessageCount + 1;
			yield _this2._connection.send(_this2._protocol.writeMessage({
				type: MessageType.Sequence,
				sequenceId
			}));
			const messages = _this2._messages;
			for (const element of messages) yield _this2._connection.send(element._message);
			_this2._reconnectInProgress = false;
		})();
	}
	_dispose(error) {
		error !== null && error !== void 0 || (error = /* @__PURE__ */ new Error("Unable to reconnect to server."));
		for (const element of this._messages) element._rejector(error);
	}
	_isInvocationMessage(message) {
		switch (message.type) {
			case MessageType.Invocation:
			case MessageType.StreamItem:
			case MessageType.Completion:
			case MessageType.StreamInvocation:
			case MessageType.CancelInvocation: return true;
			case MessageType.Close:
			case MessageType.Sequence:
			case MessageType.Ping:
			case MessageType.Ack: return false;
		}
	}
	_ackTimer() {
		var _this3 = this;
		if (this._ackTimerHandle === void 0) this._ackTimerHandle = setTimeout(_asyncToGenerator(function* () {
			try {
				if (!_this3._reconnectInProgress) yield _this3._connection.send(_this3._protocol.writeMessage({
					type: MessageType.Ack,
					sequenceId: _this3._latestReceivedSequenceId
				}));
			} catch (_unused2) {}
			clearTimeout(_this3._ackTimerHandle);
			_this3._ackTimerHandle = void 0;
		}), 1e3);
	}
};
var BufferedItem = class {
	constructor(message, id, resolver, rejector) {
		this._message = message;
		this._id = id;
		this._resolver = resolver;
		this._rejector = rejector;
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/HubConnection.js
var DEFAULT_TIMEOUT_IN_MS = 30 * 1e3;
var DEFAULT_PING_INTERVAL_IN_MS = 15 * 1e3;
var DEFAULT_STATEFUL_RECONNECT_BUFFER_SIZE = 1e5;
/** Describes the current state of the {@link HubConnection} to the server. */
var HubConnectionState;
(function(HubConnectionState) {
	/** The hub connection is disconnected. */
	HubConnectionState["Disconnected"] = "Disconnected";
	/** The hub connection is connecting. */
	HubConnectionState["Connecting"] = "Connecting";
	/** The hub connection is connected. */
	HubConnectionState["Connected"] = "Connected";
	/** The hub connection is disconnecting. */
	HubConnectionState["Disconnecting"] = "Disconnecting";
	/** The hub connection is reconnecting. */
	HubConnectionState["Reconnecting"] = "Reconnecting";
})(HubConnectionState || (HubConnectionState = {}));
/** Represents a connection to a SignalR Hub. */
var HubConnection = class HubConnection {
	/** @internal */
	static create(connection, logger, protocol, reconnectPolicy, serverTimeoutInMilliseconds, keepAliveIntervalInMilliseconds, statefulReconnectBufferSize) {
		return new HubConnection(connection, logger, protocol, reconnectPolicy, serverTimeoutInMilliseconds, keepAliveIntervalInMilliseconds, statefulReconnectBufferSize);
	}
	constructor(connection, logger, protocol, reconnectPolicy, serverTimeoutInMilliseconds, keepAliveIntervalInMilliseconds, statefulReconnectBufferSize) {
		this._nextKeepAlive = 0;
		this._freezeEventListener = () => {
			this._logger.log(LogLevel.Warning, "The page is being frozen, this will likely lead to the connection being closed and messages being lost. For more information see the docs at https://learn.microsoft.com/aspnet/core/signalr/javascript-client#bsleep");
		};
		Arg.isRequired(connection, "connection");
		Arg.isRequired(logger, "logger");
		Arg.isRequired(protocol, "protocol");
		this.serverTimeoutInMilliseconds = serverTimeoutInMilliseconds !== null && serverTimeoutInMilliseconds !== void 0 ? serverTimeoutInMilliseconds : DEFAULT_TIMEOUT_IN_MS;
		this.keepAliveIntervalInMilliseconds = keepAliveIntervalInMilliseconds !== null && keepAliveIntervalInMilliseconds !== void 0 ? keepAliveIntervalInMilliseconds : DEFAULT_PING_INTERVAL_IN_MS;
		this._statefulReconnectBufferSize = statefulReconnectBufferSize !== null && statefulReconnectBufferSize !== void 0 ? statefulReconnectBufferSize : DEFAULT_STATEFUL_RECONNECT_BUFFER_SIZE;
		this._logger = logger;
		this._protocol = protocol;
		this.connection = connection;
		this._reconnectPolicy = reconnectPolicy;
		this._handshakeProtocol = new HandshakeProtocol();
		this.connection.onreceive = (data) => this._processIncomingData(data);
		this.connection.onclose = (error) => this._connectionClosed(error);
		this._callbacks = {};
		this._methods = {};
		this._closedCallbacks = [];
		this._reconnectingCallbacks = [];
		this._reconnectedCallbacks = [];
		this._invocationId = 0;
		this._receivedHandshakeResponse = false;
		this._connectionState = HubConnectionState.Disconnected;
		this._connectionStarted = false;
		this._cachedPingMessage = this._protocol.writeMessage({ type: MessageType.Ping });
	}
	/** Indicates the state of the {@link HubConnection} to the server. */
	get state() {
		return this._connectionState;
	}
	/** Represents the connection id of the {@link HubConnection} on the server. The connection id will be null when the connection is either
	*  in the disconnected state or if the negotiation step was skipped.
	*/
	get connectionId() {
		return this.connection ? this.connection.connectionId || null : null;
	}
	/** Indicates the url of the {@link HubConnection} to the server. */
	get baseUrl() {
		return this.connection.baseUrl || "";
	}
	/**
	* Sets a new url for the HubConnection. Note that the url can only be changed when the connection is in either the Disconnected or
	* Reconnecting states.
	* @param {string} url The url to connect to.
	*/
	set baseUrl(url) {
		if (this._connectionState !== HubConnectionState.Disconnected && this._connectionState !== HubConnectionState.Reconnecting) throw new Error("The HubConnection must be in the Disconnected or Reconnecting state to change the url.");
		if (!url) throw new Error("The HubConnection url must be a valid url.");
		this.connection.baseUrl = url;
	}
	/** Starts the connection.
	*
	* @returns {Promise<void>} A Promise that resolves when the connection has been successfully established, or rejects with an error.
	*/
	start() {
		this._startPromise = this._startWithStateTransitions();
		return this._startPromise;
	}
	_startWithStateTransitions() {
		var _this = this;
		return _asyncToGenerator(function* () {
			if (_this._connectionState !== HubConnectionState.Disconnected) return Promise.reject(/* @__PURE__ */ new Error("Cannot start a HubConnection that is not in the 'Disconnected' state."));
			_this._connectionState = HubConnectionState.Connecting;
			_this._logger.log(LogLevel.Debug, "Starting HubConnection.");
			try {
				yield _this._startInternal();
				if (Platform.isBrowser) window.document.addEventListener("freeze", _this._freezeEventListener);
				_this._connectionState = HubConnectionState.Connected;
				_this._connectionStarted = true;
				_this._logger.log(LogLevel.Debug, "HubConnection connected successfully.");
			} catch (e) {
				_this._connectionState = HubConnectionState.Disconnected;
				_this._logger.log(LogLevel.Debug, `HubConnection failed to start successfully because of error '${e}'.`);
				return Promise.reject(e);
			}
		})();
	}
	_startInternal() {
		var _this2 = this;
		return _asyncToGenerator(function* () {
			_this2._stopDuringStartError = void 0;
			_this2._receivedHandshakeResponse = false;
			const handshakePromise = new Promise((resolve, reject) => {
				_this2._handshakeResolver = resolve;
				_this2._handshakeRejecter = reject;
			});
			yield _this2.connection.start(_this2._protocol.transferFormat);
			try {
				let version = _this2._protocol.version;
				if (!_this2.connection.features.reconnect) version = 1;
				const handshakeRequest = {
					protocol: _this2._protocol.name,
					version
				};
				_this2._logger.log(LogLevel.Debug, "Sending handshake request.");
				yield _this2._sendMessage(_this2._handshakeProtocol.writeHandshakeRequest(handshakeRequest));
				_this2._logger.log(LogLevel.Information, `Using HubProtocol '${_this2._protocol.name}'.`);
				_this2._cleanupTimeout();
				_this2._resetTimeoutPeriod();
				_this2._resetKeepAliveInterval();
				yield handshakePromise;
				if (_this2._stopDuringStartError) throw _this2._stopDuringStartError;
				if (_this2.connection.features.reconnect || false) {
					_this2._messageBuffer = new MessageBuffer(_this2._protocol, _this2.connection, _this2._statefulReconnectBufferSize);
					_this2.connection.features.disconnected = _this2._messageBuffer._disconnected.bind(_this2._messageBuffer);
					_this2.connection.features.resend = () => {
						if (_this2._messageBuffer) return _this2._messageBuffer._resend();
					};
				}
				if (!_this2.connection.features.inherentKeepAlive) yield _this2._sendMessage(_this2._cachedPingMessage);
			} catch (e) {
				_this2._logger.log(LogLevel.Debug, `Hub handshake failed with error '${e}' during start(). Stopping HubConnection.`);
				_this2._cleanupTimeout();
				_this2._cleanupPingTimer();
				yield _this2.connection.stop(e);
				throw e;
			}
		})();
	}
	/** Stops the connection.
	*
	* @returns {Promise<void>} A Promise that resolves when the connection has been successfully terminated, or rejects with an error.
	*/
	stop() {
		var _this3 = this;
		return _asyncToGenerator(function* () {
			const startPromise = _this3._startPromise;
			_this3.connection.features.reconnect = false;
			_this3._stopPromise = _this3._stopInternal();
			yield _this3._stopPromise;
			try {
				yield startPromise;
			} catch (e) {}
		})();
	}
	_stopInternal(error) {
		if (this._connectionState === HubConnectionState.Disconnected) {
			this._logger.log(LogLevel.Debug, `Call to HubConnection.stop(${error}) ignored because it is already in the disconnected state.`);
			return Promise.resolve();
		}
		if (this._connectionState === HubConnectionState.Disconnecting) {
			this._logger.log(LogLevel.Debug, `Call to HttpConnection.stop(${error}) ignored because the connection is already in the disconnecting state.`);
			return this._stopPromise;
		}
		const state = this._connectionState;
		this._connectionState = HubConnectionState.Disconnecting;
		this._logger.log(LogLevel.Debug, "Stopping HubConnection.");
		if (this._reconnectDelayHandle) {
			this._logger.log(LogLevel.Debug, "Connection stopped during reconnect delay. Done reconnecting.");
			clearTimeout(this._reconnectDelayHandle);
			this._reconnectDelayHandle = void 0;
			this._completeClose();
			return Promise.resolve();
		}
		if (state === HubConnectionState.Connected) this._sendCloseMessage();
		this._cleanupTimeout();
		this._cleanupPingTimer();
		this._stopDuringStartError = error || new AbortError("The connection was stopped before the hub handshake could complete.");
		return this.connection.stop(error);
	}
	_sendCloseMessage() {
		var _this4 = this;
		return _asyncToGenerator(function* () {
			try {
				yield _this4._sendWithProtocol(_this4._createCloseMessage());
			} catch (_unused) {}
		})();
	}
	/** Invokes a streaming hub method on the server using the specified name and arguments.
	*
	* @typeparam T The type of the items returned by the server.
	* @param {string} methodName The name of the server method to invoke.
	* @param {any[]} args The arguments used to invoke the server method.
	* @returns {IStreamResult<T>} An object that yields results from the server as they are received.
	*/
	stream(methodName, ...args) {
		const [streams, streamIds] = this._replaceStreamingParams(args);
		const invocationDescriptor = this._createStreamInvocation(methodName, args, streamIds);
		let promiseQueue;
		const subject = new Subject();
		subject.cancelCallback = () => {
			const cancelInvocation = this._createCancelInvocation(invocationDescriptor.invocationId);
			delete this._callbacks[invocationDescriptor.invocationId];
			return promiseQueue.then(() => {
				return this._sendWithProtocol(cancelInvocation);
			});
		};
		this._callbacks[invocationDescriptor.invocationId] = (invocationEvent, error) => {
			if (error) {
				subject.error(error);
				return;
			} else if (invocationEvent) if (invocationEvent.type === MessageType.Completion) if (invocationEvent.error) subject.error(new Error(invocationEvent.error));
			else subject.complete();
			else subject.next(invocationEvent.item);
		};
		promiseQueue = this._sendWithProtocol(invocationDescriptor).catch((e) => {
			subject.error(e);
			delete this._callbacks[invocationDescriptor.invocationId];
		});
		this._launchStreams(streams, promiseQueue);
		return subject;
	}
	_sendMessage(message) {
		this._resetKeepAliveInterval();
		return this.connection.send(message);
	}
	/**
	* Sends a js object to the server.
	* @param message The js object to serialize and send.
	*/
	_sendWithProtocol(message) {
		if (this._messageBuffer) return this._messageBuffer._send(message);
		else return this._sendMessage(this._protocol.writeMessage(message));
	}
	/** Invokes a hub method on the server using the specified name and arguments. Does not wait for a response from the receiver.
	*
	* The Promise returned by this method resolves when the client has sent the invocation to the server. The server may still
	* be processing the invocation.
	*
	* @param {string} methodName The name of the server method to invoke.
	* @param {any[]} args The arguments used to invoke the server method.
	* @returns {Promise<void>} A Promise that resolves when the invocation has been successfully sent, or rejects with an error.
	*/
	send(methodName, ...args) {
		const [streams, streamIds] = this._replaceStreamingParams(args);
		const sendPromise = this._sendWithProtocol(this._createInvocation(methodName, args, true, streamIds));
		this._launchStreams(streams, sendPromise);
		return sendPromise;
	}
	/** Invokes a hub method on the server using the specified name and arguments.
	*
	* The Promise returned by this method resolves when the server indicates it has finished invoking the method. When the promise
	* resolves, the server has finished invoking the method. If the server method returns a result, it is produced as the result of
	* resolving the Promise.
	*
	* @typeparam T The expected return type.
	* @param {string} methodName The name of the server method to invoke.
	* @param {any[]} args The arguments used to invoke the server method.
	* @returns {Promise<T>} A Promise that resolves with the result of the server method (if any), or rejects with an error.
	*/
	invoke(methodName, ...args) {
		const [streams, streamIds] = this._replaceStreamingParams(args);
		const invocationDescriptor = this._createInvocation(methodName, args, false, streamIds);
		return new Promise((resolve, reject) => {
			this._callbacks[invocationDescriptor.invocationId] = (invocationEvent, error) => {
				if (error) {
					reject(error);
					return;
				} else if (invocationEvent) if (invocationEvent.type === MessageType.Completion) if (invocationEvent.error) reject(new Error(invocationEvent.error));
				else resolve(invocationEvent.result);
				else reject(/* @__PURE__ */ new Error(`Unexpected message type: ${invocationEvent.type}`));
			};
			const promiseQueue = this._sendWithProtocol(invocationDescriptor).catch((e) => {
				reject(e);
				delete this._callbacks[invocationDescriptor.invocationId];
			});
			this._launchStreams(streams, promiseQueue);
		});
	}
	on(methodName, newMethod) {
		if (!methodName || !newMethod) return;
		methodName = methodName.toLowerCase();
		if (!this._methods[methodName]) this._methods[methodName] = [];
		if (this._methods[methodName].indexOf(newMethod) !== -1) return;
		this._methods[methodName].push(newMethod);
	}
	off(methodName, method) {
		if (!methodName) return;
		methodName = methodName.toLowerCase();
		const handlers = this._methods[methodName];
		if (!handlers) return;
		if (method) {
			const removeIdx = handlers.indexOf(method);
			if (removeIdx !== -1) {
				handlers.splice(removeIdx, 1);
				if (handlers.length === 0) delete this._methods[methodName];
			}
		} else delete this._methods[methodName];
	}
	/** Registers a handler that will be invoked when the connection is closed.
	*
	* @param {Function} callback The handler that will be invoked when the connection is closed. Optionally receives a single argument containing the error that caused the connection to close (if any).
	*/
	onclose(callback) {
		if (callback) this._closedCallbacks.push(callback);
	}
	/** Registers a handler that will be invoked when the connection starts reconnecting.
	*
	* @param {Function} callback The handler that will be invoked when the connection starts reconnecting. Optionally receives a single argument containing the error that caused the connection to start reconnecting (if any).
	*/
	onreconnecting(callback) {
		if (callback) this._reconnectingCallbacks.push(callback);
	}
	/** Registers a handler that will be invoked when the connection successfully reconnects.
	*
	* @param {Function} callback The handler that will be invoked when the connection successfully reconnects.
	*/
	onreconnected(callback) {
		if (callback) this._reconnectedCallbacks.push(callback);
	}
	_processIncomingData(data) {
		this._cleanupTimeout();
		if (!this._receivedHandshakeResponse) {
			data = this._processHandshakeResponse(data);
			this._receivedHandshakeResponse = true;
		}
		if (data) {
			const messages = this._protocol.parseMessages(data, this._logger);
			for (const message of messages) {
				if (this._messageBuffer && !this._messageBuffer._shouldProcessMessage(message)) continue;
				switch (message.type) {
					case MessageType.Invocation:
						this._invokeClientMethod(message).catch((e) => {
							this._logger.log(LogLevel.Error, `Invoke client method threw error: ${getErrorString(e)}`);
						});
						break;
					case MessageType.StreamItem:
					case MessageType.Completion: {
						const callback = this._callbacks[message.invocationId];
						if (callback) {
							if (message.type === MessageType.Completion) delete this._callbacks[message.invocationId];
							try {
								callback(message);
							} catch (e) {
								this._logger.log(LogLevel.Error, `Stream callback threw error: ${getErrorString(e)}`);
							}
						}
						break;
					}
					case MessageType.Ping: break;
					case MessageType.Close: {
						this._logger.log(LogLevel.Information, "Close message received from server.");
						const error = message.error ? /* @__PURE__ */ new Error("Server returned an error on close: " + message.error) : void 0;
						if (message.allowReconnect === true) this.connection.stop(error);
						else this._stopPromise = this._stopInternal(error);
						break;
					}
					case MessageType.Ack:
						if (this._messageBuffer) this._messageBuffer._ack(message);
						break;
					case MessageType.Sequence:
						if (this._messageBuffer) this._messageBuffer._resetSequence(message);
						break;
					default:
						this._logger.log(LogLevel.Warning, `Invalid message type: ${message.type}.`);
						break;
				}
			}
		}
		this._resetTimeoutPeriod();
	}
	_processHandshakeResponse(data) {
		let responseMessage;
		let remainingData;
		try {
			[remainingData, responseMessage] = this._handshakeProtocol.parseHandshakeResponse(data);
		} catch (e) {
			const message = "Error parsing handshake response: " + e;
			this._logger.log(LogLevel.Error, message);
			const error = new Error(message);
			this._handshakeRejecter(error);
			throw error;
		}
		if (responseMessage.error) {
			const message = "Server returned handshake error: " + responseMessage.error;
			this._logger.log(LogLevel.Error, message);
			const error = new Error(message);
			this._handshakeRejecter(error);
			throw error;
		} else this._logger.log(LogLevel.Debug, "Server handshake complete.");
		this._handshakeResolver();
		return remainingData;
	}
	_resetKeepAliveInterval() {
		if (this.connection.features.inherentKeepAlive) return;
		this._nextKeepAlive = (/* @__PURE__ */ new Date()).getTime() + this.keepAliveIntervalInMilliseconds;
		this._cleanupPingTimer();
	}
	_resetTimeoutPeriod() {
		var _this5 = this;
		if (!this.connection.features || !this.connection.features.inherentKeepAlive) {
			this._timeoutHandle = setTimeout(() => this.serverTimeout(), this.serverTimeoutInMilliseconds);
			let nextPing = this._nextKeepAlive - (/* @__PURE__ */ new Date()).getTime();
			if (nextPing < 0) {
				if (this._connectionState === HubConnectionState.Connected) this._trySendPingMessage();
				return;
			}
			if (this._pingServerHandle === void 0) {
				if (nextPing < 0) nextPing = 0;
				this._pingServerHandle = setTimeout(_asyncToGenerator(function* () {
					if (_this5._connectionState === HubConnectionState.Connected) yield _this5._trySendPingMessage();
				}), nextPing);
			}
		}
	}
	serverTimeout() {
		this.connection.stop(/* @__PURE__ */ new Error("Server timeout elapsed without receiving a message from the server."));
	}
	_invokeClientMethod(invocationMessage) {
		var _this6 = this;
		return _asyncToGenerator(function* () {
			const methodName = invocationMessage.target.toLowerCase();
			const methods = _this6._methods[methodName];
			if (!methods) {
				_this6._logger.log(LogLevel.Warning, `No client method with the name '${methodName}' found.`);
				if (invocationMessage.invocationId) {
					_this6._logger.log(LogLevel.Warning, `No result given for '${methodName}' method and invocation ID '${invocationMessage.invocationId}'.`);
					yield _this6._sendWithProtocol(_this6._createCompletionMessage(invocationMessage.invocationId, "Client didn't provide a result.", null));
				}
				return;
			}
			const methodsCopy = methods.slice();
			const expectsResponse = invocationMessage.invocationId ? true : false;
			let res;
			let exception;
			let completionMessage;
			for (const m of methodsCopy) try {
				const prevRes = res;
				res = yield m.apply(_this6, invocationMessage.arguments);
				if (expectsResponse && res && prevRes) {
					_this6._logger.log(LogLevel.Error, `Multiple results provided for '${methodName}'. Sending error to server.`);
					completionMessage = _this6._createCompletionMessage(invocationMessage.invocationId, `Client provided multiple results.`, null);
				}
				exception = void 0;
			} catch (e) {
				exception = e;
				_this6._logger.log(LogLevel.Error, `A callback for the method '${methodName}' threw error '${e}'.`);
			}
			if (completionMessage) yield _this6._sendWithProtocol(completionMessage);
			else if (expectsResponse) {
				if (exception) completionMessage = _this6._createCompletionMessage(invocationMessage.invocationId, `${exception}`, null);
				else if (res !== void 0) completionMessage = _this6._createCompletionMessage(invocationMessage.invocationId, null, res);
				else {
					_this6._logger.log(LogLevel.Warning, `No result given for '${methodName}' method and invocation ID '${invocationMessage.invocationId}'.`);
					completionMessage = _this6._createCompletionMessage(invocationMessage.invocationId, "Client didn't provide a result.", null);
				}
				yield _this6._sendWithProtocol(completionMessage);
			} else if (res) _this6._logger.log(LogLevel.Error, `Result given for '${methodName}' method but server is not expecting a result.`);
		})();
	}
	_connectionClosed(error) {
		this._logger.log(LogLevel.Debug, `HubConnection.connectionClosed(${error}) called while in state ${this._connectionState}.`);
		this._stopDuringStartError = this._stopDuringStartError || error || new AbortError("The underlying connection was closed before the hub handshake could complete.");
		if (this._handshakeResolver) this._handshakeResolver();
		this._cancelCallbacksWithError(error || /* @__PURE__ */ new Error("Invocation canceled due to the underlying connection being closed."));
		this._cleanupTimeout();
		this._cleanupPingTimer();
		if (this._connectionState === HubConnectionState.Disconnecting) this._completeClose(error);
		else if (this._connectionState === HubConnectionState.Connected && this._reconnectPolicy) this._reconnect(error);
		else if (this._connectionState === HubConnectionState.Connected) this._completeClose(error);
	}
	_completeClose(error) {
		if (this._connectionStarted) {
			this._connectionState = HubConnectionState.Disconnected;
			this._connectionStarted = false;
			if (this._messageBuffer) {
				this._messageBuffer._dispose(error !== null && error !== void 0 ? error : /* @__PURE__ */ new Error("Connection closed."));
				this._messageBuffer = void 0;
			}
			if (Platform.isBrowser) window.document.removeEventListener("freeze", this._freezeEventListener);
			try {
				this._closedCallbacks.forEach((c) => c.apply(this, [error]));
			} catch (e) {
				this._logger.log(LogLevel.Error, `An onclose callback called with error '${error}' threw error '${e}'.`);
			}
		}
	}
	_reconnect(error) {
		var _this7 = this;
		return _asyncToGenerator(function* () {
			const reconnectStartTime = Date.now();
			let previousReconnectAttempts = 0;
			let retryError = error !== void 0 ? error : /* @__PURE__ */ new Error("Attempting to reconnect due to a unknown error.");
			let nextRetryDelay = _this7._getNextRetryDelay(previousReconnectAttempts, 0, retryError);
			if (nextRetryDelay === null) {
				_this7._logger.log(LogLevel.Debug, "Connection not reconnecting because the IRetryPolicy returned null on the first reconnect attempt.");
				_this7._completeClose(error);
				return;
			}
			_this7._connectionState = HubConnectionState.Reconnecting;
			if (error) _this7._logger.log(LogLevel.Information, `Connection reconnecting because of error '${error}'.`);
			else _this7._logger.log(LogLevel.Information, "Connection reconnecting.");
			if (_this7._reconnectingCallbacks.length !== 0) {
				try {
					_this7._reconnectingCallbacks.forEach((c) => c.apply(_this7, [error]));
				} catch (e) {
					_this7._logger.log(LogLevel.Error, `An onreconnecting callback called with error '${error}' threw error '${e}'.`);
				}
				if (_this7._connectionState !== HubConnectionState.Reconnecting) {
					_this7._logger.log(LogLevel.Debug, "Connection left the reconnecting state in onreconnecting callback. Done reconnecting.");
					return;
				}
			}
			while (nextRetryDelay !== null) {
				_this7._logger.log(LogLevel.Information, `Reconnect attempt number ${previousReconnectAttempts + 1} will start in ${nextRetryDelay} ms.`);
				yield new Promise((resolve) => {
					_this7._reconnectDelayHandle = setTimeout(resolve, nextRetryDelay);
				});
				_this7._reconnectDelayHandle = void 0;
				if (_this7._connectionState !== HubConnectionState.Reconnecting) {
					_this7._logger.log(LogLevel.Debug, "Connection left the reconnecting state during reconnect delay. Done reconnecting.");
					return;
				}
				try {
					yield _this7._startInternal();
					_this7._connectionState = HubConnectionState.Connected;
					_this7._logger.log(LogLevel.Information, "HubConnection reconnected successfully.");
					if (_this7._reconnectedCallbacks.length !== 0) try {
						_this7._reconnectedCallbacks.forEach((c) => c.apply(_this7, [_this7.connection.connectionId]));
					} catch (e) {
						_this7._logger.log(LogLevel.Error, `An onreconnected callback called with connectionId '${_this7.connection.connectionId}; threw error '${e}'.`);
					}
					return;
				} catch (e) {
					_this7._logger.log(LogLevel.Information, `Reconnect attempt failed because of error '${e}'.`);
					if (_this7._connectionState !== HubConnectionState.Reconnecting) {
						_this7._logger.log(LogLevel.Debug, `Connection moved to the '${_this7._connectionState}' from the reconnecting state during reconnect attempt. Done reconnecting.`);
						if (_this7._connectionState === HubConnectionState.Disconnecting) _this7._completeClose();
						return;
					}
					previousReconnectAttempts++;
					retryError = e instanceof Error ? e : new Error(e.toString());
					nextRetryDelay = _this7._getNextRetryDelay(previousReconnectAttempts, Date.now() - reconnectStartTime, retryError);
				}
			}
			_this7._logger.log(LogLevel.Information, `Reconnect retries have been exhausted after ${Date.now() - reconnectStartTime} ms and ${previousReconnectAttempts} failed attempts. Connection disconnecting.`);
			_this7._completeClose();
		})();
	}
	_getNextRetryDelay(previousRetryCount, elapsedMilliseconds, retryReason) {
		try {
			return this._reconnectPolicy.nextRetryDelayInMilliseconds({
				elapsedMilliseconds,
				previousRetryCount,
				retryReason
			});
		} catch (e) {
			this._logger.log(LogLevel.Error, `IRetryPolicy.nextRetryDelayInMilliseconds(${previousRetryCount}, ${elapsedMilliseconds}) threw error '${e}'.`);
			return null;
		}
	}
	_cancelCallbacksWithError(error) {
		const callbacks = this._callbacks;
		this._callbacks = {};
		Object.keys(callbacks).forEach((key) => {
			const callback = callbacks[key];
			try {
				callback(null, error);
			} catch (e) {
				this._logger.log(LogLevel.Error, `Stream 'error' callback called with '${error}' threw error: ${getErrorString(e)}`);
			}
		});
	}
	_cleanupPingTimer() {
		if (this._pingServerHandle) {
			clearTimeout(this._pingServerHandle);
			this._pingServerHandle = void 0;
		}
	}
	_cleanupTimeout() {
		if (this._timeoutHandle) clearTimeout(this._timeoutHandle);
	}
	_createInvocation(methodName, args, nonblocking, streamIds) {
		if (nonblocking) if (streamIds.length !== 0) return {
			target: methodName,
			arguments: args,
			streamIds,
			type: MessageType.Invocation
		};
		else return {
			target: methodName,
			arguments: args,
			type: MessageType.Invocation
		};
		else {
			const invocationId = this._invocationId;
			this._invocationId++;
			if (streamIds.length !== 0) return {
				target: methodName,
				arguments: args,
				invocationId: invocationId.toString(),
				streamIds,
				type: MessageType.Invocation
			};
			else return {
				target: methodName,
				arguments: args,
				invocationId: invocationId.toString(),
				type: MessageType.Invocation
			};
		}
	}
	_launchStreams(streams, promiseQueue) {
		if (streams.length === 0) return;
		if (!promiseQueue) promiseQueue = Promise.resolve();
		for (const streamId in streams) streams[streamId].subscribe({
			complete: () => {
				promiseQueue = promiseQueue.then(() => this._sendWithProtocol(this._createCompletionMessage(streamId)));
			},
			error: (err) => {
				let message;
				if (err instanceof Error) message = err.message;
				else if (err && err.toString) message = err.toString();
				else message = "Unknown error";
				promiseQueue = promiseQueue.then(() => this._sendWithProtocol(this._createCompletionMessage(streamId, message)));
			},
			next: (item) => {
				promiseQueue = promiseQueue.then(() => this._sendWithProtocol(this._createStreamItemMessage(streamId, item)));
			}
		});
	}
	_replaceStreamingParams(args) {
		const streams = [];
		const streamIds = [];
		for (let i = 0; i < args.length; i++) {
			const argument = args[i];
			if (this._isObservable(argument)) {
				const streamId = this._invocationId;
				this._invocationId++;
				streams[streamId] = argument;
				streamIds.push(streamId.toString());
				args.splice(i, 1);
			}
		}
		return [streams, streamIds];
	}
	_isObservable(arg) {
		return arg && arg.subscribe && typeof arg.subscribe === "function";
	}
	_createStreamInvocation(methodName, args, streamIds) {
		const invocationId = this._invocationId;
		this._invocationId++;
		if (streamIds.length !== 0) return {
			target: methodName,
			arguments: args,
			invocationId: invocationId.toString(),
			streamIds,
			type: MessageType.StreamInvocation
		};
		else return {
			target: methodName,
			arguments: args,
			invocationId: invocationId.toString(),
			type: MessageType.StreamInvocation
		};
	}
	_createCancelInvocation(id) {
		return {
			invocationId: id,
			type: MessageType.CancelInvocation
		};
	}
	_createStreamItemMessage(id, item) {
		return {
			invocationId: id,
			item,
			type: MessageType.StreamItem
		};
	}
	_createCompletionMessage(id, error, result) {
		if (error) return {
			error,
			invocationId: id,
			type: MessageType.Completion
		};
		return {
			invocationId: id,
			result,
			type: MessageType.Completion
		};
	}
	_createCloseMessage() {
		return { type: MessageType.Close };
	}
	_trySendPingMessage() {
		var _this8 = this;
		return _asyncToGenerator(function* () {
			try {
				yield _this8._sendMessage(_this8._cachedPingMessage);
			} catch (_unused2) {
				_this8._cleanupPingTimer();
			}
		})();
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/DefaultReconnectPolicy.js
var DEFAULT_RETRY_DELAYS_IN_MILLISECONDS = [
	0,
	2e3,
	1e4,
	3e4,
	null
];
/** @private */
var DefaultReconnectPolicy = class {
	constructor(retryDelays) {
		this._retryDelays = retryDelays !== void 0 ? [...retryDelays, null] : DEFAULT_RETRY_DELAYS_IN_MILLISECONDS;
	}
	nextRetryDelayInMilliseconds(retryContext) {
		return this._retryDelays[retryContext.previousRetryCount];
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/HeaderNames.js
var HeaderNames = class {};
HeaderNames.Authorization = "Authorization";
HeaderNames.Cookie = "Cookie";
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/AccessTokenHttpClient.js
/** @private */
var AccessTokenHttpClient = class extends HttpClient {
	constructor(innerClient, accessTokenFactory) {
		super();
		this._innerClient = innerClient;
		this._accessTokenFactory = accessTokenFactory;
	}
	send(request) {
		var _this = this;
		return _asyncToGenerator(function* () {
			let allowRetry = true;
			if (_this._accessTokenFactory && (!_this._accessToken || request.url && request.url.indexOf("/negotiate?") > 0)) {
				allowRetry = false;
				_this._accessToken = yield _this._accessTokenFactory();
			}
			_this._setAuthorizationHeader(request);
			const response = yield _this._innerClient.send(request);
			if (allowRetry && response.statusCode === 401 && _this._accessTokenFactory) {
				_this._accessToken = yield _this._accessTokenFactory();
				_this._setAuthorizationHeader(request);
				return yield _this._innerClient.send(request);
			}
			return response;
		})();
	}
	_setAuthorizationHeader(request) {
		if (!request.headers) request.headers = {};
		if (this._accessToken) request.headers[HeaderNames.Authorization] = `Bearer ${this._accessToken}`;
		else if (this._accessTokenFactory) {
			if (request.headers[HeaderNames.Authorization]) delete request.headers[HeaderNames.Authorization];
		}
	}
	getCookieString(url) {
		return this._innerClient.getCookieString(url);
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/ITransport.js
/** Specifies a specific HTTP transport type. */
var HttpTransportType;
(function(HttpTransportType) {
	/** Specifies no transport preference. */
	HttpTransportType[HttpTransportType["None"] = 0] = "None";
	/** Specifies the WebSockets transport. */
	HttpTransportType[HttpTransportType["WebSockets"] = 1] = "WebSockets";
	/** Specifies the Server-Sent Events transport. */
	HttpTransportType[HttpTransportType["ServerSentEvents"] = 2] = "ServerSentEvents";
	/** Specifies the Long Polling transport. */
	HttpTransportType[HttpTransportType["LongPolling"] = 4] = "LongPolling";
})(HttpTransportType || (HttpTransportType = {}));
/** Specifies the transfer format for a connection. */
var TransferFormat;
(function(TransferFormat) {
	/** Specifies that only text data will be transmitted over the connection. */
	TransferFormat[TransferFormat["Text"] = 1] = "Text";
	/** Specifies that binary data will be transmitted over the connection. */
	TransferFormat[TransferFormat["Binary"] = 2] = "Binary";
})(TransferFormat || (TransferFormat = {}));
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/AbortController.js
/** @private */
var AbortController$1 = class {
	constructor() {
		this._isAborted = false;
		this.onabort = null;
	}
	abort() {
		if (!this._isAborted) {
			this._isAborted = true;
			if (this.onabort) this.onabort();
		}
	}
	get signal() {
		return this;
	}
	get aborted() {
		return this._isAborted;
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/LongPollingTransport.js
/** @private */
var LongPollingTransport = class {
	get pollAborted() {
		return this._pollAbort.aborted;
	}
	constructor(httpClient, logger, options) {
		this._httpClient = httpClient;
		this._logger = logger;
		this._pollAbort = new AbortController$1();
		this._options = options;
		this._running = false;
		this.onreceive = null;
		this.onclose = null;
	}
	connect(url, transferFormat) {
		var _this = this;
		return _asyncToGenerator(function* () {
			Arg.isRequired(url, "url");
			Arg.isRequired(transferFormat, "transferFormat");
			Arg.isIn(transferFormat, TransferFormat, "transferFormat");
			_this._url = url;
			_this._logger.log(LogLevel.Trace, "(LongPolling transport) Connecting.");
			if (transferFormat === TransferFormat.Binary && typeof XMLHttpRequest !== "undefined" && typeof new XMLHttpRequest().responseType !== "string") throw new Error("Binary protocols over XmlHttpRequest not implementing advanced features are not supported.");
			const [name, value] = getUserAgentHeader();
			const headers = _objectSpread2({ [name]: value }, _this._options.headers);
			const pollOptions = {
				abortSignal: _this._pollAbort.signal,
				headers,
				timeout: 1e5,
				withCredentials: _this._options.withCredentials
			};
			if (transferFormat === TransferFormat.Binary) pollOptions.responseType = "arraybuffer";
			const pollUrl = `${url}&_=${Date.now()}`;
			_this._logger.log(LogLevel.Trace, `(LongPolling transport) polling: ${pollUrl}.`);
			const response = yield _this._httpClient.get(pollUrl, pollOptions);
			if (response.statusCode !== 200) {
				_this._logger.log(LogLevel.Error, `(LongPolling transport) Unexpected response code: ${response.statusCode}.`);
				_this._closeError = new HttpError(response.statusText || "", response.statusCode);
				_this._running = false;
			} else _this._running = true;
			_this._receiving = _this._poll(_this._url, pollOptions);
		})();
	}
	_poll(url, pollOptions) {
		var _this2 = this;
		return _asyncToGenerator(function* () {
			try {
				while (_this2._running) try {
					const pollUrl = `${url}&_=${Date.now()}`;
					_this2._logger.log(LogLevel.Trace, `(LongPolling transport) polling: ${pollUrl}.`);
					const response = yield _this2._httpClient.get(pollUrl, pollOptions);
					if (response.statusCode === 204) {
						_this2._logger.log(LogLevel.Information, "(LongPolling transport) Poll terminated by server.");
						_this2._running = false;
					} else if (response.statusCode !== 200) {
						_this2._logger.log(LogLevel.Error, `(LongPolling transport) Unexpected response code: ${response.statusCode}.`);
						_this2._closeError = new HttpError(response.statusText || "", response.statusCode);
						_this2._running = false;
					} else if (response.content) {
						_this2._logger.log(LogLevel.Trace, `(LongPolling transport) data received. ${getDataDetail(response.content, _this2._options.logMessageContent)}.`);
						if (_this2.onreceive) _this2.onreceive(response.content);
					} else _this2._logger.log(LogLevel.Trace, "(LongPolling transport) Poll timed out, reissuing.");
				} catch (e) {
					if (!_this2._running) _this2._logger.log(LogLevel.Trace, `(LongPolling transport) Poll errored after shutdown: ${e.message}`);
					else if (e instanceof TimeoutError) _this2._logger.log(LogLevel.Trace, "(LongPolling transport) Poll timed out, reissuing.");
					else {
						_this2._closeError = e;
						_this2._running = false;
					}
				}
			} finally {
				_this2._logger.log(LogLevel.Trace, "(LongPolling transport) Polling complete.");
				if (!_this2.pollAborted) _this2._raiseOnClose();
			}
		})();
	}
	send(data) {
		var _this3 = this;
		return _asyncToGenerator(function* () {
			if (!_this3._running) return Promise.reject(/* @__PURE__ */ new Error("Cannot send until the transport is connected"));
			return sendMessage(_this3._logger, "LongPolling", _this3._httpClient, _this3._url, data, _this3._options);
		})();
	}
	stop() {
		var _this4 = this;
		return _asyncToGenerator(function* () {
			_this4._logger.log(LogLevel.Trace, "(LongPolling transport) Stopping polling.");
			_this4._running = false;
			_this4._pollAbort.abort();
			try {
				yield _this4._receiving;
				_this4._logger.log(LogLevel.Trace, `(LongPolling transport) sending DELETE request to ${_this4._url}.`);
				const headers = {};
				const [name, value] = getUserAgentHeader();
				headers[name] = value;
				const deleteOptions = {
					headers: _objectSpread2(_objectSpread2({}, headers), _this4._options.headers),
					timeout: _this4._options.timeout,
					withCredentials: _this4._options.withCredentials
				};
				let error;
				try {
					yield _this4._httpClient.delete(_this4._url, deleteOptions);
				} catch (err) {
					error = err;
				}
				if (error) {
					if (error instanceof HttpError) if (error.statusCode === 404) _this4._logger.log(LogLevel.Trace, "(LongPolling transport) A 404 response was returned from sending a DELETE request.");
					else _this4._logger.log(LogLevel.Trace, `(LongPolling transport) Error sending a DELETE request: ${error}`);
				} else _this4._logger.log(LogLevel.Trace, "(LongPolling transport) DELETE request accepted.");
			} finally {
				_this4._logger.log(LogLevel.Trace, "(LongPolling transport) Stop finished.");
				_this4._raiseOnClose();
			}
		})();
	}
	_raiseOnClose() {
		if (this.onclose) {
			let logMessage = "(LongPolling transport) Firing onclose event.";
			if (this._closeError) logMessage += " Error: " + this._closeError;
			this._logger.log(LogLevel.Trace, logMessage);
			this.onclose(this._closeError);
		}
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/ServerSentEventsTransport.js
/** @private */
var ServerSentEventsTransport = class {
	constructor(httpClient, accessToken, logger, options) {
		this._httpClient = httpClient;
		this._accessToken = accessToken;
		this._logger = logger;
		this._options = options;
		this.onreceive = null;
		this.onclose = null;
	}
	connect(url, transferFormat) {
		var _this = this;
		return _asyncToGenerator(function* () {
			Arg.isRequired(url, "url");
			Arg.isRequired(transferFormat, "transferFormat");
			Arg.isIn(transferFormat, TransferFormat, "transferFormat");
			_this._logger.log(LogLevel.Trace, "(SSE transport) Connecting.");
			_this._url = url;
			if (_this._accessToken) url += (url.indexOf("?") < 0 ? "?" : "&") + `access_token=${encodeURIComponent(_this._accessToken)}`;
			return new Promise((resolve, reject) => {
				let opened = false;
				if (transferFormat !== TransferFormat.Text) {
					reject(/* @__PURE__ */ new Error("The Server-Sent Events transport only supports the 'Text' transfer format"));
					return;
				}
				let eventSource;
				if (Platform.isBrowser || Platform.isWebWorker) eventSource = new _this._options.EventSource(url, { withCredentials: _this._options.withCredentials });
				else {
					const cookies = _this._httpClient.getCookieString(url);
					const headers = {};
					headers.Cookie = cookies;
					const [name, value] = getUserAgentHeader();
					headers[name] = value;
					eventSource = new _this._options.EventSource(url, {
						withCredentials: _this._options.withCredentials,
						headers: _objectSpread2(_objectSpread2({}, headers), _this._options.headers)
					});
				}
				try {
					eventSource.onmessage = (e) => {
						if (_this.onreceive) try {
							_this._logger.log(LogLevel.Trace, `(SSE transport) data received. ${getDataDetail(e.data, _this._options.logMessageContent)}.`);
							_this.onreceive(e.data);
						} catch (error) {
							_this._close(error);
							return;
						}
					};
					eventSource.onerror = (e) => {
						if (opened) _this._close();
						else reject(/* @__PURE__ */ new Error("EventSource failed to connect. The connection could not be found on the server, either the connection ID is not present on the server, or a proxy is refusing/buffering the connection. If you have multiple servers check that sticky sessions are enabled."));
					};
					eventSource.onopen = () => {
						_this._logger.log(LogLevel.Information, `SSE connected to ${_this._url}`);
						_this._eventSource = eventSource;
						opened = true;
						resolve();
					};
				} catch (e) {
					reject(e);
					return;
				}
			});
		})();
	}
	send(data) {
		var _this2 = this;
		return _asyncToGenerator(function* () {
			if (!_this2._eventSource) return Promise.reject(/* @__PURE__ */ new Error("Cannot send until the transport is connected"));
			return sendMessage(_this2._logger, "SSE", _this2._httpClient, _this2._url, data, _this2._options);
		})();
	}
	stop() {
		this._close();
		return Promise.resolve();
	}
	_close(e) {
		if (this._eventSource) {
			this._eventSource.close();
			this._eventSource = void 0;
			if (this.onclose) this.onclose(e);
		}
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/WebSocketTransport.js
/** @private */
var WebSocketTransport = class {
	constructor(httpClient, accessTokenFactory, logger, logMessageContent, webSocketConstructor, headers) {
		this._logger = logger;
		this._accessTokenFactory = accessTokenFactory;
		this._logMessageContent = logMessageContent;
		this._webSocketConstructor = webSocketConstructor;
		this._httpClient = httpClient;
		this.onreceive = null;
		this.onclose = null;
		this._headers = headers;
	}
	connect(url, transferFormat) {
		var _this = this;
		return _asyncToGenerator(function* () {
			Arg.isRequired(url, "url");
			Arg.isRequired(transferFormat, "transferFormat");
			Arg.isIn(transferFormat, TransferFormat, "transferFormat");
			_this._logger.log(LogLevel.Trace, "(WebSockets transport) Connecting.");
			let token;
			if (_this._accessTokenFactory) token = yield _this._accessTokenFactory();
			return new Promise((resolve, reject) => {
				url = url.replace(/^http/, "ws");
				let webSocket;
				const cookies = _this._httpClient.getCookieString(url);
				let opened = false;
				if (Platform.isNode || Platform.isReactNative) {
					const headers = {};
					const [name, value] = getUserAgentHeader();
					headers[name] = value;
					if (token) headers[HeaderNames.Authorization] = `Bearer ${token}`;
					if (cookies) headers[HeaderNames.Cookie] = cookies;
					webSocket = new _this._webSocketConstructor(url, void 0, { headers: _objectSpread2(_objectSpread2({}, headers), _this._headers) });
				} else if (token) url += (url.indexOf("?") < 0 ? "?" : "&") + `access_token=${encodeURIComponent(token)}`;
				if (!webSocket) webSocket = new _this._webSocketConstructor(url);
				if (transferFormat === TransferFormat.Binary) webSocket.binaryType = "arraybuffer";
				webSocket.onopen = (_event) => {
					_this._logger.log(LogLevel.Information, `WebSocket connected to ${url}.`);
					_this._webSocket = webSocket;
					opened = true;
					resolve();
				};
				webSocket.onerror = (event) => {
					let error = null;
					if (typeof ErrorEvent !== "undefined" && event instanceof ErrorEvent) error = event.error;
					else error = "There was an error with the transport";
					_this._logger.log(LogLevel.Information, `(WebSockets transport) ${error}.`);
				};
				webSocket.onmessage = (message) => {
					_this._logger.log(LogLevel.Trace, `(WebSockets transport) data received. ${getDataDetail(message.data, _this._logMessageContent)}.`);
					if (_this.onreceive) try {
						_this.onreceive(message.data);
					} catch (error) {
						_this._close(error);
						return;
					}
				};
				webSocket.onclose = (event) => {
					if (opened) _this._close(event);
					else {
						let error = null;
						if (typeof ErrorEvent !== "undefined" && event instanceof ErrorEvent) error = event.error;
						else error = "WebSocket failed to connect. The connection could not be found on the server, either the endpoint may not be a SignalR endpoint, the connection ID is not present on the server, or there is a proxy blocking WebSockets. If you have multiple servers check that sticky sessions are enabled.";
						reject(new Error(error));
					}
				};
			});
		})();
	}
	send(data) {
		if (this._webSocket && this._webSocket.readyState === this._webSocketConstructor.OPEN) {
			this._logger.log(LogLevel.Trace, `(WebSockets transport) sending data. ${getDataDetail(data, this._logMessageContent)}.`);
			this._webSocket.send(data);
			return Promise.resolve();
		}
		return Promise.reject("WebSocket is not in the OPEN state");
	}
	stop() {
		if (this._webSocket) this._close(void 0);
		return Promise.resolve();
	}
	_close(event) {
		if (this._webSocket) {
			this._webSocket.onclose = () => {};
			this._webSocket.onmessage = () => {};
			this._webSocket.onerror = () => {};
			this._webSocket.close();
			this._webSocket = void 0;
		}
		this._logger.log(LogLevel.Trace, "(WebSockets transport) socket closed.");
		if (this.onclose) if (this._isCloseEvent(event) && (event.wasClean === false || event.code !== 1e3)) this.onclose(/* @__PURE__ */ new Error(`WebSocket closed with status code: ${event.code} (${event.reason || "no reason given"}).`));
		else if (event instanceof Error) this.onclose(event);
		else this.onclose();
	}
	_isCloseEvent(event) {
		return event && typeof event.wasClean === "boolean" && typeof event.code === "number";
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/HttpConnection.js
var MAX_REDIRECTS = 100;
/** @private */
var HttpConnection = class {
	constructor(url, options = {}) {
		this._stopPromiseResolver = () => {};
		this.features = {};
		this._negotiateVersion = 1;
		Arg.isRequired(url, "url");
		this._logger = createLogger(options.logger);
		this.baseUrl = this._resolveUrl(url);
		options = options || {};
		options.logMessageContent = options.logMessageContent === void 0 ? false : options.logMessageContent;
		if (typeof options.withCredentials === "boolean" || options.withCredentials === void 0) options.withCredentials = options.withCredentials === void 0 ? true : options.withCredentials;
		else throw new Error("withCredentials option was not a 'boolean' or 'undefined' value");
		options.timeout = options.timeout === void 0 ? 100 * 1e3 : options.timeout;
		let webSocketModule = null;
		let eventSourceModule = null;
		if (Platform.isNode && typeof __require !== "undefined") {
			const requireFunc = typeof __webpack_require__ === "function" ? __non_webpack_require__ : __require;
			webSocketModule = requireFunc("ws");
			eventSourceModule = requireFunc("eventsource");
		}
		if (!Platform.isNode && typeof WebSocket !== "undefined" && !options.WebSocket) options.WebSocket = WebSocket;
		else if (Platform.isNode && !options.WebSocket) {
			if (webSocketModule) options.WebSocket = webSocketModule;
		}
		if (!Platform.isNode && typeof EventSource !== "undefined" && !options.EventSource) options.EventSource = EventSource;
		else if (Platform.isNode && !options.EventSource) {
			if (typeof eventSourceModule !== "undefined") options.EventSource = eventSourceModule;
		}
		this._httpClient = new AccessTokenHttpClient(options.httpClient || new DefaultHttpClient(this._logger), options.accessTokenFactory);
		this._connectionState = "Disconnected";
		this._connectionStarted = false;
		this._options = options;
		this.onreceive = null;
		this.onclose = null;
	}
	start(transferFormat) {
		var _this = this;
		return _asyncToGenerator(function* () {
			transferFormat = transferFormat || TransferFormat.Binary;
			Arg.isIn(transferFormat, TransferFormat, "transferFormat");
			_this._logger.log(LogLevel.Debug, `Starting connection with transfer format '${TransferFormat[transferFormat]}'.`);
			if (_this._connectionState !== "Disconnected") return Promise.reject(/* @__PURE__ */ new Error("Cannot start an HttpConnection that is not in the 'Disconnected' state."));
			_this._connectionState = "Connecting";
			_this._startInternalPromise = _this._startInternal(transferFormat);
			yield _this._startInternalPromise;
			if (_this._connectionState === "Disconnecting") {
				const message = "Failed to start the HttpConnection before stop() was called.";
				_this._logger.log(LogLevel.Error, message);
				yield _this._stopPromise;
				return Promise.reject(new AbortError(message));
			} else if (_this._connectionState !== "Connected") {
				const message = "HttpConnection.startInternal completed gracefully but didn't enter the connection into the connected state!";
				_this._logger.log(LogLevel.Error, message);
				return Promise.reject(new AbortError(message));
			}
			_this._connectionStarted = true;
		})();
	}
	send(data) {
		if (this._connectionState !== "Connected") return Promise.reject(/* @__PURE__ */ new Error("Cannot send data if the connection is not in the 'Connected' State."));
		if (!this._sendQueue) this._sendQueue = new TransportSendQueue(this.transport);
		return this._sendQueue.send(data);
	}
	stop(error) {
		var _this2 = this;
		return _asyncToGenerator(function* () {
			if (_this2._connectionState === "Disconnected") {
				_this2._logger.log(LogLevel.Debug, `Call to HttpConnection.stop(${error}) ignored because the connection is already in the disconnected state.`);
				return Promise.resolve();
			}
			if (_this2._connectionState === "Disconnecting") {
				_this2._logger.log(LogLevel.Debug, `Call to HttpConnection.stop(${error}) ignored because the connection is already in the disconnecting state.`);
				return _this2._stopPromise;
			}
			_this2._connectionState = "Disconnecting";
			_this2._stopPromise = new Promise((resolve) => {
				_this2._stopPromiseResolver = resolve;
			});
			yield _this2._stopInternal(error);
			yield _this2._stopPromise;
		})();
	}
	_stopInternal(error) {
		var _this3 = this;
		return _asyncToGenerator(function* () {
			_this3._stopError = error;
			try {
				yield _this3._startInternalPromise;
			} catch (e) {}
			if (_this3.transport) {
				try {
					yield _this3.transport.stop();
				} catch (e) {
					_this3._logger.log(LogLevel.Error, `HttpConnection.transport.stop() threw error '${e}'.`);
					_this3._stopConnection();
				}
				_this3.transport = void 0;
			} else _this3._logger.log(LogLevel.Debug, "HttpConnection.transport is undefined in HttpConnection.stop() because start() failed.");
		})();
	}
	_startInternal(transferFormat) {
		var _this4 = this;
		return _asyncToGenerator(function* () {
			let url = _this4.baseUrl;
			_this4._accessTokenFactory = _this4._options.accessTokenFactory;
			_this4._httpClient._accessTokenFactory = _this4._accessTokenFactory;
			try {
				if (_this4._options.skipNegotiation) if (_this4._options.transport === HttpTransportType.WebSockets) {
					_this4.transport = _this4._constructTransport(HttpTransportType.WebSockets);
					yield _this4._startTransport(url, transferFormat);
				} else throw new Error("Negotiation can only be skipped when using the WebSocket transport directly.");
				else {
					let negotiateResponse = null;
					let redirects = 0;
					do {
						negotiateResponse = yield _this4._getNegotiationResponse(url);
						if (_this4._connectionState === "Disconnecting" || _this4._connectionState === "Disconnected") throw new AbortError("The connection was stopped during negotiation.");
						if (negotiateResponse.error) throw new Error(negotiateResponse.error);
						if (negotiateResponse.ProtocolVersion) throw new Error("Detected a connection attempt to an ASP.NET SignalR Server. This client only supports connecting to an ASP.NET Core SignalR Server. See https://aka.ms/signalr-core-differences for details.");
						if (negotiateResponse.url) url = negotiateResponse.url;
						if (negotiateResponse.accessToken) {
							const accessToken = negotiateResponse.accessToken;
							_this4._accessTokenFactory = () => accessToken;
							_this4._httpClient._accessToken = accessToken;
							_this4._httpClient._accessTokenFactory = void 0;
						}
						redirects++;
					} while (negotiateResponse.url && redirects < MAX_REDIRECTS);
					if (redirects === MAX_REDIRECTS && negotiateResponse.url) throw new Error("Negotiate redirection limit exceeded.");
					yield _this4._createTransport(url, _this4._options.transport, negotiateResponse, transferFormat);
				}
				if (_this4.transport instanceof LongPollingTransport) _this4.features.inherentKeepAlive = true;
				if (_this4._connectionState === "Connecting") {
					_this4._logger.log(LogLevel.Debug, "The HttpConnection connected successfully.");
					_this4._connectionState = "Connected";
				}
			} catch (e) {
				_this4._logger.log(LogLevel.Error, "Failed to start the connection: " + e);
				_this4._connectionState = "Disconnected";
				_this4.transport = void 0;
				_this4._stopPromiseResolver();
				return Promise.reject(e);
			}
		})();
	}
	_getNegotiationResponse(url) {
		var _this5 = this;
		return _asyncToGenerator(function* () {
			const headers = {};
			const [name, value] = getUserAgentHeader();
			headers[name] = value;
			const negotiateUrl = _this5._resolveNegotiateUrl(url);
			_this5._logger.log(LogLevel.Debug, `Sending negotiation request: ${negotiateUrl}.`);
			try {
				const response = yield _this5._httpClient.post(negotiateUrl, {
					content: "",
					headers: _objectSpread2(_objectSpread2({}, headers), _this5._options.headers),
					timeout: _this5._options.timeout,
					withCredentials: _this5._options.withCredentials
				});
				if (response.statusCode !== 200) return Promise.reject(/* @__PURE__ */ new Error(`Unexpected status code returned from negotiate '${response.statusCode}'`));
				const negotiateResponse = JSON.parse(response.content);
				if (!negotiateResponse.negotiateVersion || negotiateResponse.negotiateVersion < 1) negotiateResponse.connectionToken = negotiateResponse.connectionId;
				if (negotiateResponse.useStatefulReconnect && _this5._options._useStatefulReconnect !== true) return Promise.reject(new FailedToNegotiateWithServerError("Client didn't negotiate Stateful Reconnect but the server did."));
				return negotiateResponse;
			} catch (e) {
				let errorMessage = "Failed to complete negotiation with the server: " + e;
				if (e instanceof HttpError) {
					if (e.statusCode === 404) errorMessage = errorMessage + " Either this is not a SignalR endpoint or there is a proxy blocking the connection.";
				}
				_this5._logger.log(LogLevel.Error, errorMessage);
				return Promise.reject(new FailedToNegotiateWithServerError(errorMessage));
			}
		})();
	}
	_createConnectUrl(url, connectionToken) {
		if (!connectionToken) return url;
		return url + (url.indexOf("?") === -1 ? "?" : "&") + `id=${connectionToken}`;
	}
	_createTransport(url, requestedTransport, negotiateResponse, requestedTransferFormat) {
		var _this6 = this;
		return _asyncToGenerator(function* () {
			let connectUrl = _this6._createConnectUrl(url, negotiateResponse.connectionToken);
			if (_this6._isITransport(requestedTransport)) {
				_this6._logger.log(LogLevel.Debug, "Connection was provided an instance of ITransport, using that directly.");
				_this6.transport = requestedTransport;
				yield _this6._startTransport(connectUrl, requestedTransferFormat);
				_this6.connectionId = negotiateResponse.connectionId;
				return;
			}
			const transportExceptions = [];
			const transports = negotiateResponse.availableTransports || [];
			let negotiate = negotiateResponse;
			for (const endpoint of transports) {
				const transportOrError = _this6._resolveTransportOrError(endpoint, requestedTransport, requestedTransferFormat, (negotiate === null || negotiate === void 0 ? void 0 : negotiate.useStatefulReconnect) === true);
				if (transportOrError instanceof Error) {
					transportExceptions.push(`${endpoint.transport} failed:`);
					transportExceptions.push(transportOrError);
				} else if (_this6._isITransport(transportOrError)) {
					_this6.transport = transportOrError;
					if (!negotiate) {
						try {
							negotiate = yield _this6._getNegotiationResponse(url);
						} catch (ex) {
							return Promise.reject(ex);
						}
						connectUrl = _this6._createConnectUrl(url, negotiate.connectionToken);
					}
					try {
						yield _this6._startTransport(connectUrl, requestedTransferFormat);
						_this6.connectionId = negotiate.connectionId;
						return;
					} catch (ex) {
						_this6._logger.log(LogLevel.Error, `Failed to start the transport '${endpoint.transport}': ${ex}`);
						negotiate = void 0;
						transportExceptions.push(new FailedToStartTransportError(`${endpoint.transport} failed: ${ex}`, HttpTransportType[endpoint.transport]));
						if (_this6._connectionState !== "Connecting") {
							const message = "Failed to select transport before stop() was called.";
							_this6._logger.log(LogLevel.Debug, message);
							return Promise.reject(new AbortError(message));
						}
					}
				}
			}
			if (transportExceptions.length > 0) return Promise.reject(new AggregateErrors(`Unable to connect to the server with any of the available transports. ${transportExceptions.join(" ")}`, transportExceptions));
			return Promise.reject(/* @__PURE__ */ new Error("None of the transports supported by the client are supported by the server."));
		})();
	}
	_constructTransport(transport) {
		switch (transport) {
			case HttpTransportType.WebSockets:
				if (!this._options.WebSocket) throw new Error("'WebSocket' is not supported in your environment.");
				return new WebSocketTransport(this._httpClient, this._accessTokenFactory, this._logger, this._options.logMessageContent, this._options.WebSocket, this._options.headers || {});
			case HttpTransportType.ServerSentEvents:
				if (!this._options.EventSource) throw new Error("'EventSource' is not supported in your environment.");
				return new ServerSentEventsTransport(this._httpClient, this._httpClient._accessToken, this._logger, this._options);
			case HttpTransportType.LongPolling: return new LongPollingTransport(this._httpClient, this._logger, this._options);
			default: throw new Error(`Unknown transport: ${transport}.`);
		}
	}
	_startTransport(url, transferFormat) {
		var _this7 = this;
		this.transport.onreceive = this.onreceive;
		if (this.features.reconnect) this.transport.onclose = function() {
			var _ref = _asyncToGenerator(function* (e) {
				let callStop = false;
				if (_this7.features.reconnect) try {
					_this7.features.disconnected();
					yield _this7.transport.connect(url, transferFormat);
					yield _this7.features.resend();
				} catch (_unused) {
					callStop = true;
				}
				else {
					_this7._stopConnection(e);
					return;
				}
				if (callStop) _this7._stopConnection(e);
			});
			return function(_x) {
				return _ref.apply(this, arguments);
			};
		}();
		else this.transport.onclose = (e) => this._stopConnection(e);
		return this.transport.connect(url, transferFormat);
	}
	_resolveTransportOrError(endpoint, requestedTransport, requestedTransferFormat, useStatefulReconnect) {
		const transport = HttpTransportType[endpoint.transport];
		if (transport === null || transport === void 0) {
			this._logger.log(LogLevel.Debug, `Skipping transport '${endpoint.transport}' because it is not supported by this client.`);
			return /* @__PURE__ */ new Error(`Skipping transport '${endpoint.transport}' because it is not supported by this client.`);
		} else if (transportMatches(requestedTransport, transport)) if (endpoint.transferFormats.map((s) => TransferFormat[s]).indexOf(requestedTransferFormat) >= 0) if (transport === HttpTransportType.WebSockets && !this._options.WebSocket || transport === HttpTransportType.ServerSentEvents && !this._options.EventSource) {
			this._logger.log(LogLevel.Debug, `Skipping transport '${HttpTransportType[transport]}' because it is not supported in your environment.'`);
			return new UnsupportedTransportError(`'${HttpTransportType[transport]}' is not supported in your environment.`, transport);
		} else {
			this._logger.log(LogLevel.Debug, `Selecting transport '${HttpTransportType[transport]}'.`);
			try {
				this.features.reconnect = transport === HttpTransportType.WebSockets ? useStatefulReconnect : void 0;
				return this._constructTransport(transport);
			} catch (ex) {
				return ex;
			}
		}
		else {
			this._logger.log(LogLevel.Debug, `Skipping transport '${HttpTransportType[transport]}' because it does not support the requested transfer format '${TransferFormat[requestedTransferFormat]}'.`);
			return /* @__PURE__ */ new Error(`'${HttpTransportType[transport]}' does not support ${TransferFormat[requestedTransferFormat]}.`);
		}
		else {
			this._logger.log(LogLevel.Debug, `Skipping transport '${HttpTransportType[transport]}' because it was disabled by the client.`);
			return new DisabledTransportError(`'${HttpTransportType[transport]}' is disabled by the client.`, transport);
		}
	}
	_isITransport(transport) {
		return transport && typeof transport === "object" && "connect" in transport;
	}
	_stopConnection(error) {
		this._logger.log(LogLevel.Debug, `HttpConnection.stopConnection(${error}) called while in state ${this._connectionState}.`);
		this.transport = void 0;
		error = this._stopError || error;
		this._stopError = void 0;
		if (this._connectionState === "Disconnected") {
			this._logger.log(LogLevel.Debug, `Call to HttpConnection.stopConnection(${error}) was ignored because the connection is already in the disconnected state.`);
			return;
		}
		if (this._connectionState === "Connecting") {
			this._logger.log(LogLevel.Warning, `Call to HttpConnection.stopConnection(${error}) was ignored because the connection is still in the connecting state.`);
			throw new Error(`HttpConnection.stopConnection(${error}) was called while the connection is still in the connecting state.`);
		}
		if (this._connectionState === "Disconnecting") this._stopPromiseResolver();
		if (error) this._logger.log(LogLevel.Error, `Connection disconnected with error '${error}'.`);
		else this._logger.log(LogLevel.Information, "Connection disconnected.");
		if (this._sendQueue) {
			this._sendQueue.stop().catch((e) => {
				this._logger.log(LogLevel.Error, `TransportSendQueue.stop() threw error '${e}'.`);
			});
			this._sendQueue = void 0;
		}
		this.connectionId = void 0;
		this._connectionState = "Disconnected";
		if (this._connectionStarted) {
			this._connectionStarted = false;
			try {
				if (this.onclose) this.onclose(error);
			} catch (e) {
				this._logger.log(LogLevel.Error, `HttpConnection.onclose(${error}) threw error '${e}'.`);
			}
		}
	}
	_resolveUrl(url) {
		if (url.lastIndexOf("https://", 0) === 0 || url.lastIndexOf("http://", 0) === 0) return url;
		if (!Platform.isBrowser) throw new Error(`Cannot resolve '${url}'.`);
		const aTag = window.document.createElement("a");
		aTag.href = url;
		this._logger.log(LogLevel.Information, `Normalizing '${url}' to '${aTag.href}'.`);
		return aTag.href;
	}
	_resolveNegotiateUrl(url) {
		const negotiateUrl = new URL(url);
		if (negotiateUrl.pathname.endsWith("/")) negotiateUrl.pathname += "negotiate";
		else negotiateUrl.pathname += "/negotiate";
		const searchParams = new URLSearchParams(negotiateUrl.searchParams);
		if (!searchParams.has("negotiateVersion")) searchParams.append("negotiateVersion", this._negotiateVersion.toString());
		if (searchParams.has("useStatefulReconnect")) {
			if (searchParams.get("useStatefulReconnect") === "true") this._options._useStatefulReconnect = true;
		} else if (this._options._useStatefulReconnect === true) searchParams.append("useStatefulReconnect", "true");
		negotiateUrl.search = searchParams.toString();
		return negotiateUrl.toString();
	}
};
function transportMatches(requestedTransport, actualTransport) {
	return !requestedTransport || (actualTransport & requestedTransport) !== 0;
}
/** @private */
var TransportSendQueue = class TransportSendQueue {
	constructor(_transport) {
		this._transport = _transport;
		this._buffer = [];
		this._executing = true;
		this._sendBufferedData = new PromiseSource();
		this._transportResult = new PromiseSource();
		this._sendLoopPromise = this._sendLoop();
	}
	send(data) {
		this._bufferData(data);
		if (!this._transportResult) this._transportResult = new PromiseSource();
		return this._transportResult.promise;
	}
	stop() {
		this._executing = false;
		this._sendBufferedData.resolve();
		return this._sendLoopPromise;
	}
	_bufferData(data) {
		if (this._buffer.length && typeof this._buffer[0] !== typeof data) throw new Error(`Expected data to be of type ${typeof this._buffer} but was of type ${typeof data}`);
		this._buffer.push(data);
		this._sendBufferedData.resolve();
	}
	_sendLoop() {
		var _this8 = this;
		return _asyncToGenerator(function* () {
			while (true) {
				yield _this8._sendBufferedData.promise;
				if (!_this8._executing) {
					if (_this8._transportResult) _this8._transportResult.reject("Connection stopped.");
					break;
				}
				_this8._sendBufferedData = new PromiseSource();
				const transportResult = _this8._transportResult;
				_this8._transportResult = void 0;
				const data = typeof _this8._buffer[0] === "string" ? _this8._buffer.join("") : TransportSendQueue._concatBuffers(_this8._buffer);
				_this8._buffer.length = 0;
				try {
					yield _this8._transport.send(data);
					transportResult.resolve();
				} catch (error) {
					transportResult.reject(error);
				}
			}
		})();
	}
	static _concatBuffers(arrayBuffers) {
		const totalLength = arrayBuffers.map((b) => b.byteLength).reduce((a, b) => a + b);
		const result = new Uint8Array(totalLength);
		let offset = 0;
		for (const item of arrayBuffers) {
			result.set(new Uint8Array(item), offset);
			offset += item.byteLength;
		}
		return result.buffer;
	}
};
var PromiseSource = class {
	constructor() {
		this.promise = new Promise((resolve, reject) => [this._resolver, this._rejecter] = [resolve, reject]);
	}
	resolve() {
		this._resolver();
	}
	reject(reason) {
		this._rejecter(reason);
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/JsonHubProtocol.js
var JSON_HUB_PROTOCOL_NAME = "json";
/** Implements the JSON Hub Protocol. */
var JsonHubProtocol = class {
	constructor() {
		/** @inheritDoc */
		this.name = JSON_HUB_PROTOCOL_NAME;
		/** @inheritDoc */
		this.version = 2;
		/** @inheritDoc */
		this.transferFormat = TransferFormat.Text;
	}
	/** Creates an array of {@link @microsoft/signalr.HubMessage} objects from the specified serialized representation.
	*
	* @param {string} input A string containing the serialized representation.
	* @param {ILogger} logger A logger that will be used to log messages that occur during parsing.
	*/
	parseMessages(input, logger) {
		if (typeof input !== "string") throw new Error("Invalid input for JSON hub protocol. Expected a string.");
		if (!input) return [];
		if (logger === null) logger = NullLogger.instance;
		const messages = TextMessageFormat.parse(input);
		const hubMessages = [];
		for (const message of messages) {
			const parsedMessage = JSON.parse(message);
			if (typeof parsedMessage.type !== "number") throw new Error("Invalid payload.");
			switch (parsedMessage.type) {
				case MessageType.Invocation:
					this._isInvocationMessage(parsedMessage);
					break;
				case MessageType.StreamItem:
					this._isStreamItemMessage(parsedMessage);
					break;
				case MessageType.Completion:
					this._isCompletionMessage(parsedMessage);
					break;
				case MessageType.Ping: break;
				case MessageType.Close: break;
				case MessageType.Ack:
					this._isAckMessage(parsedMessage);
					break;
				case MessageType.Sequence:
					this._isSequenceMessage(parsedMessage);
					break;
				default:
					logger.log(LogLevel.Information, "Unknown message type '" + parsedMessage.type + "' ignored.");
					continue;
			}
			hubMessages.push(parsedMessage);
		}
		return hubMessages;
	}
	/** Writes the specified {@link @microsoft/signalr.HubMessage} to a string and returns it.
	*
	* @param {HubMessage} message The message to write.
	* @returns {string} A string containing the serialized representation of the message.
	*/
	writeMessage(message) {
		return TextMessageFormat.write(JSON.stringify(message));
	}
	_isInvocationMessage(message) {
		this._assertNotEmptyString(message.target, "Invalid payload for Invocation message.");
		if (message.invocationId !== void 0) this._assertNotEmptyString(message.invocationId, "Invalid payload for Invocation message.");
	}
	_isStreamItemMessage(message) {
		this._assertNotEmptyString(message.invocationId, "Invalid payload for StreamItem message.");
		if (message.item === void 0) throw new Error("Invalid payload for StreamItem message.");
	}
	_isCompletionMessage(message) {
		if (message.result && message.error) throw new Error("Invalid payload for Completion message.");
		if (!message.result && message.error) this._assertNotEmptyString(message.error, "Invalid payload for Completion message.");
		this._assertNotEmptyString(message.invocationId, "Invalid payload for Completion message.");
	}
	_isAckMessage(message) {
		if (typeof message.sequenceId !== "number") throw new Error("Invalid SequenceId for Ack message.");
	}
	_isSequenceMessage(message) {
		if (typeof message.sequenceId !== "number") throw new Error("Invalid SequenceId for Sequence message.");
	}
	_assertNotEmptyString(value, errorMessage) {
		if (typeof value !== "string" || value === "") throw new Error(errorMessage);
	}
};
//#endregion
//#region node_modules/@microsoft/signalr/dist/esm/HubConnectionBuilder.js
var LogLevelNameMapping = {
	trace: LogLevel.Trace,
	debug: LogLevel.Debug,
	info: LogLevel.Information,
	information: LogLevel.Information,
	warn: LogLevel.Warning,
	warning: LogLevel.Warning,
	error: LogLevel.Error,
	critical: LogLevel.Critical,
	none: LogLevel.None
};
function parseLogLevel(name) {
	const mapping = LogLevelNameMapping[name.toLowerCase()];
	if (typeof mapping !== "undefined") return mapping;
	else throw new Error(`Unknown log level: ${name}`);
}
/** A builder for configuring {@link @microsoft/signalr.HubConnection} instances. */
var HubConnectionBuilder = class {
	configureLogging(logging) {
		Arg.isRequired(logging, "logging");
		if (isLogger(logging)) this.logger = logging;
		else if (typeof logging === "string") {
			const logLevel = parseLogLevel(logging);
			this.logger = new ConsoleLogger(logLevel);
		} else this.logger = new ConsoleLogger(logging);
		return this;
	}
	withUrl(url, transportTypeOrOptions) {
		Arg.isRequired(url, "url");
		Arg.isNotEmpty(url, "url");
		this.url = url;
		if (typeof transportTypeOrOptions === "object") this.httpConnectionOptions = _objectSpread2(_objectSpread2({}, this.httpConnectionOptions), transportTypeOrOptions);
		else this.httpConnectionOptions = _objectSpread2(_objectSpread2({}, this.httpConnectionOptions), {}, { transport: transportTypeOrOptions });
		return this;
	}
	/** Configures the {@link @microsoft/signalr.HubConnection} to use the specified Hub Protocol.
	*
	* @param {IHubProtocol} protocol The {@link @microsoft/signalr.IHubProtocol} implementation to use.
	*/
	withHubProtocol(protocol) {
		Arg.isRequired(protocol, "protocol");
		this.protocol = protocol;
		return this;
	}
	withAutomaticReconnect(retryDelaysOrReconnectPolicy) {
		if (this.reconnectPolicy) throw new Error("A reconnectPolicy has already been set.");
		if (!retryDelaysOrReconnectPolicy) this.reconnectPolicy = new DefaultReconnectPolicy();
		else if (Array.isArray(retryDelaysOrReconnectPolicy)) this.reconnectPolicy = new DefaultReconnectPolicy(retryDelaysOrReconnectPolicy);
		else this.reconnectPolicy = retryDelaysOrReconnectPolicy;
		return this;
	}
	/** Configures {@link @microsoft/signalr.HubConnection.serverTimeoutInMilliseconds} for the {@link @microsoft/signalr.HubConnection}.
	*
	* @returns The {@link @microsoft/signalr.HubConnectionBuilder} instance, for chaining.
	*/
	withServerTimeout(milliseconds) {
		Arg.isRequired(milliseconds, "milliseconds");
		this._serverTimeoutInMilliseconds = milliseconds;
		return this;
	}
	/** Configures {@link @microsoft/signalr.HubConnection.keepAliveIntervalInMilliseconds} for the {@link @microsoft/signalr.HubConnection}.
	*
	* @returns The {@link @microsoft/signalr.HubConnectionBuilder} instance, for chaining.
	*/
	withKeepAliveInterval(milliseconds) {
		Arg.isRequired(milliseconds, "milliseconds");
		this._keepAliveIntervalInMilliseconds = milliseconds;
		return this;
	}
	/** Enables and configures options for the Stateful Reconnect feature.
	*
	* @returns The {@link @microsoft/signalr.HubConnectionBuilder} instance, for chaining.
	*/
	withStatefulReconnect(options) {
		if (this.httpConnectionOptions === void 0) this.httpConnectionOptions = {};
		this.httpConnectionOptions._useStatefulReconnect = true;
		this._statefulReconnectBufferSize = options === null || options === void 0 ? void 0 : options.bufferSize;
		return this;
	}
	/** Creates a {@link @microsoft/signalr.HubConnection} from the configuration options specified in this builder.
	*
	* @returns {HubConnection} The configured {@link @microsoft/signalr.HubConnection}.
	*/
	build() {
		const httpConnectionOptions = this.httpConnectionOptions || {};
		if (httpConnectionOptions.logger === void 0) httpConnectionOptions.logger = this.logger;
		if (!this.url) throw new Error("The 'HubConnectionBuilder.withUrl' method must be called before building the connection.");
		const connection = new HttpConnection(this.url, httpConnectionOptions);
		return HubConnection.create(connection, this.logger || NullLogger.instance, this.protocol || new JsonHubProtocol(), this.reconnectPolicy, this._serverTimeoutInMilliseconds, this._keepAliveIntervalInMilliseconds, this._statefulReconnectBufferSize);
	}
};
function isLogger(logger) {
	return logger.log !== void 0;
}
//#endregion
export { AbortError, DefaultHttpClient, HttpClient, HttpError, HttpResponse, HttpTransportType, HubConnection, HubConnectionBuilder, HubConnectionState, JsonHubProtocol, LogLevel, MessageType, NullLogger, Subject, TimeoutError, TransferFormat, VERSION };
