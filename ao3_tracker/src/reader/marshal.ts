namespace Ao3Track {
    Helper = {} as Ao3Track.IAo3TrackHelper;

    export namespace Marshal {
        export type TypeConverter = (value: any) => any;
        export type Type = TypeConverter;

        export interface IMemberDef {
            return?: Type;
            args?: { [key: number]: Type };
            getter?: Type | boolean;
            setter?: Type | boolean;
            promise?: number;
            getterfunc?: string;
            setterfunc?: string;
        }
        export interface IHelperDef {
            [key: string]: IMemberDef;
        }
        export interface IPromise<T>
        {
            then(onComplete: (value:T)=>void, onError?: (reason:any)=>void) : IPromise<T>;
        }
        export namespace Converters {
            export let ToJSON = JSON.stringify;
            export let FromJSON = JSON.parse;
        }
        (Ao3Track.Marshal.Converters as any)["true"] = true;
        (Ao3Track.Marshal.Converters as any)["false"] = false;

        export function MarshalNativeHelper(helperDef: IHelperDef | string, nativeHelper: { [key: string]: any }) {
            Ao3Track.Helper = {} as any;
            if (typeof helperDef === "string") {
                helperDef = JSON.parse(helperDef) as IHelperDef;
            }

            let convs = Ao3Track.Marshal.Converters as any as {[key:string]:(value:any)=>any};

            for (let name in helperDef) {
                let def = helperDef[name];

                if (typeof def.return === "string") {
                    def.return = convs[def.return as any];
                }

                if (typeof def.getter === "string")
                    def.getter = convs[def.getter as any];

                if (typeof def.setter === "string")
                    def.setter = convs[def.setter as any];

                // It's a function!
                if (def.args !== undefined) {
                    let func = (nativeHelper[name] as Function).bind(nativeHelper) as Function;

                    for (let i in def.args) {
                        if (typeof def.args[i] === "string")
                            def.args[i] = convs[def.args[i] as any];
                    }

                    if (def.return || Object.keys(def.args).length > 0 || def.promise !== undefined) {
                        let argconv = def.args;
                        let retconv = def.return || null;
                        (Ao3Track.Helper as any)[name] = function () {
                            let args: any[] = [].slice.call(arguments);
                            for (let i in argconv) {
                                args[i] = argconv[i](args[i]);
                            }
                            let promcb = (def.promise !== undefined) ? args.splice(def.promise, 1)[0] : null;
                            let ret = func.apply(nativeHelper, args);
                            if (promcb) {
                                let prom = ret as IPromise<any>;
                                prom.then((v) => {
                                    if (retconv) v = retconv(v);
                                    promcb(v);
                                });
                                return;
                            }
                            else {
                                if (retconv) ret = retconv(ret);
                                return ret;
                            }
                        };
                    }
                    else {
                        (Ao3Track.Helper as any)[name] = func;
                    }
                }
                // It's a property
                else if (def.getter || def.setter) {
                    let newprop: PropertyDescriptor = { enumerable: true };

                    if (def.getter) {

                        if (def.getterfunc) {
                            let func = (nativeHelper[def.getterfunc] as Function).bind(nativeHelper) as () => any;
                            if (typeof def.getter === "function") {
                                let getter = def.getter;
                                newprop.get = () => getter(func());
                            }
                            else {
                                newprop.get = func;
                            }
                        }
                        else {
                            if (typeof def.getter === "function") {
                                let getter = def.getter;
                                newprop.get = () => getter(nativeHelper[name]);
                            }
                            else {
                                newprop.get = () => nativeHelper[name];
                            }
                        }
                    }
                    if (def.setter) {
                        if (def.setterfunc) {
                            let func = (nativeHelper[def.setterfunc] as Function).bind(nativeHelper) as (v: any) => void;
                            if (typeof def.setter === "function") {
                                let setter = def.setter;
                                newprop.set = (v) => func(setter(v));
                            }
                            else {
                                newprop.set = func;
                            }
                        }
                        else {
                            if (typeof def.setter === "function") {
                                let setter = def.setter;
                                newprop.set = (v) => nativeHelper[name] = setter(v);
                            }
                            else {
                                newprop.set = (v) => nativeHelper[name] = v;
                            }
                        }
                    }
                    Object.defineProperty(Ao3Track.Helper, name, newprop);
                }
            }
        }
    }
}