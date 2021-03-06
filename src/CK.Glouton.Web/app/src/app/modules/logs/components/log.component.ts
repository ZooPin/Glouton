import { Component, OnInit } from '@angular/core';
import { Input } from '@angular/core/';
import { ILogViewModel } from 'app/common/logs/models';
import { Output } from '@angular/core';
import { EventEmitter } from '@angular/core';
import { DialogModule } from 'primeng/primeng';
import { LogService } from 'app/_services';
import { LogType } from 'app/common/logs/models';
import { QueryParametersSnapshotService } from '../services';
import { MessageService } from 'primeng/components/common/messageservice';

@Component({
    selector: 'log',
    templateUrl: 'log.component.html',
    styleUrls: ['log.component.css']
})

export class LogComponent {

    @Input('data')
    log: ILogViewModel;

    private display: boolean = false;
    private displayChildren: boolean = false;

    constructor(
        private logService: LogService,
        private queryParamertersSnapshotService: QueryParametersSnapshotService,
        private messageService : MessageService
    ) {
    }

    marginLeft(): number {
        return this.log.groupDepth * 15;
    }

    getColor(): string {
        if (this.log.logLevel.indexOf('Fatal') >= 0) { return '#2d2e30'; }
        if (this.log.logLevel.indexOf('Warn') >= 0) { return '#f0ad4e'; }
        if (this.log.logLevel.indexOf('Info') >= 0) { return '#5bc0de'; }
        if (this.log.logLevel.indexOf('Trace') >= 0) { return '#a4a4a5'; }
        if (this.log.logLevel.indexOf('Error') >= 0) { return '#d9534f'; }
        if (this.log.logLevel.indexOf('Debug') >= 0) { return '#701ac0'; }
    }

    getText(): string {
        const maxLength: number = 80 - this.log.groupDepth * 3;
        if (!this.log.text) { return ''; }
        if (this.log.text.length > maxLength) { return this.log.text.substr(0, maxLength) + ' [...]'; }
        return this.log.text;
    }

    isOpenGroup(log: ILogViewModel): boolean {
        return log.logType === LogType.OpenGroup;
    }

    onClick(): void {
        this.display = true;
    }

    onLogClick(log: ILogViewModel): void {
        this.displayChildren = !this.displayChildren;
        if (log.children !== undefined) { return; }
        this.logService.filter(
            {
                appName: this.queryParamertersSnapshotService.appNames,
                keyword: this.queryParamertersSnapshotService.keyword,
                logLevel: this.queryParamertersSnapshotService.level,
                from: this.log.logTime,
                to: this.queryParamertersSnapshotService.dateRange.to,
                groupDepth: this.log.groupDepth + 1
            }
        ).subscribe(l => {
            log.children = l;
        },
        error => this.messageService.add({
            severity : 'error', summary : 'Error', detail : 'Error while trying to get the log' 
        }));
    }
}