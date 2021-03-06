/*
Copyright 2017 Alexis Ryan

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

namespace Ao3Track {
    namespace ReadingList {

        interface IReadingList {
            last_sync: timestamp;
            paths: { [key: string]: { uri: string; timestamp: timestamp } };
        }

        let readingListBacking: IReadingList = {
            last_sync: 0,
            paths: {}
        };

        let readingListSynced = false;
        let readingListSyncing = false;
        let onreadinglistsync: Array<(success: boolean) => void> = [];

        function do_onreadinglistsync(success: boolean) {
            for (let i = 0; i < onreadinglistsync.length; i++) {
                onreadinglistsync[i](success);
            }
            onreadinglistsync = [];
        }

        function syncToServerAsync(): Promise<boolean> {
            readingListSyncing = true;
            return new Promise<boolean>((resolve) => {
                let srl: IServerReadingList = { last_sync: readingListBacking.last_sync, paths: {} };
                for (let uri in readingListBacking.paths) {
                    let rle = readingListBacking.paths[uri];
                    srl.paths[uri] = rle.timestamp;
                }

                Ao3Track.SyncReadingListAsync(srl).then((srl) => {
                    if (srl === null) {
                        readingListSyncing = false;
                        do_onreadinglistsync(false);
                        resolve(false);
                        return;
                    }

                    for (let uri in srl.paths) {
                        let v = srl.paths[uri];
                        if (v === -1) {
                            delete readingListBacking.paths[uri];
                        }
                        else {
                            if (readingListBacking.paths[uri] !== undefined) {
                                readingListBacking.paths[uri].timestamp = v;
                            }
                            else {
                                readingListBacking.paths[uri] = { uri: uri, timestamp: v };
                            }
                        }
                    }
                    readingListBacking.last_sync = srl.last_sync;
                    readingListSynced = true;
                    readingListSyncing = false;
                    do_onreadinglistsync(true);
                    resolve(true);
                });
            });
        }

        function syncIfNeededAsync(): Promise<boolean> {
            return new Promise<boolean>((resolve) => {
                if (Ao3Track.syncingDisabled()) {
                    resolve(readingListSynced);
                    return;
                }

                if (readingListSyncing) {
                    onreadinglistsync.push((result) => {
                        resolve(result);
                        return;
                    });
                    return;
                }

                if (readingListSynced) {
                    resolve(true);
                    return;
                }

                syncToServerAsync().then((result) => {
                    resolve(result);
                    return;
                });
            });
        }

        //  Quick and dirty add to reading list
        export function add(href: string | URL): void {
            let uri = Ao3Track.Data.readingListlUri(href);
            if (uri === null) {
                return;
            }
            if (Object.keys(readingListBacking.paths).indexOf(uri.href) === -1) {
                readingListBacking.paths[uri.href] = { uri: uri.href, timestamp: Date.now() };
                syncToServerAsync();
            }
        }

        // Context menus!
        if (chrome.contextMenus.create) {
            chrome.contextMenus.create({
                id: "Ao3Track-RL-Add",
                title: "Add To Reading List",
                contexts: ["link"],
                targetUrlPatterns: ["*://archiveofourown.org/*", "*://*.archiveofourown.org/*"],
                onclick: (info, tab) => {
                    if (info.linkUrl) {
                        let url = new URL(info.linkUrl);
                        add(url);
                    }
                }
            });
        }

        Ao3Track.addMessageHandler((request: ReadingListMessageRequest) => {
            switch (request.type) {
                case "RL_URLSINLIST":
                    {
                        syncIfNeededAsync().then((result) => {
                            let response: { [key: string]: boolean; } = {};
                            for (let href of request.data) {
                                let uri = Ao3Track.Data.readingListlUri(href);
                                if (uri !== null && Object.keys(readingListBacking.paths).indexOf(uri.href) !== -1) {
                                    response[href] = true;
                                }
                                else {
                                    response[href] = false;
                                }
                            }
                            request.sendResponse(response);
                        });
                    }
                    return true;

                case "RL_WORKINLIST":
                    {
                        syncIfNeededAsync().then((result) => {
                            let response: { [key: number]: boolean; } = {};
                            for (let workid of request.data) {
                                let uri = Ao3Track.Data.readingListlUri("http://archiveofourown.org/works/" + workid);
                                if (uri !== null && Object.keys(readingListBacking.paths).indexOf(uri.href) !== -1) {
                                    response[workid] = true;
                                }
                            }
                            request.sendResponse(response);
                        });
                    }
                    return true;

            default:
                //assertNever(request);
                    
            }
            return false;
        });


    }
}

