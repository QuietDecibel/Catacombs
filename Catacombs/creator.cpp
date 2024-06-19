#include "creator.h"
#include "ui_creator.h"
#include <QFile>
#include <stdio.h>

Creator::Creator(QWidget *parent) :
    QMainWindow(parent),
    ui(new Ui::Creator)
{
    ui->setupUi(this);
}

Creator::~Creator()
{
    delete ui;
}

QString Creator::getQuestion()
{
    QString Que;
    QFile Questions("Questions.txt");
    if(!Questions.open(QIODevice::ReadOnly | QIODevice::Text)){
       return NULL;
    }
    while(!Questions.atEnd()){
        QByteArray line = Questions.readLine();
        Que = line;
    }
    return Que;
}

QString Creator::getAnswer()
{
    QString Que;
    QFile Questions("Questions.txt");
    if(!Questions.open(QIODevice::ReadOnly | QIODevice::Text)){
       return NULL;
    }
    while(!Questions.atEnd()){
        QByteArray line = Questions.readLine();
        Que = line;
    }
    return Que;
}

int Creator::getTimesClicked()
{
    return timesClicked;
}

void Creator::on_QuestionButton_clicked()
{
    //Changes the Question Buttons number and Text
    //Question Button Clicked. Change Answers to next set of answers and the Question in the Dialogue box, and the Question number.
    if(getTimesClicked() == 11){
        timesClicked = 0;
    };
    if(getTimesClicked() >= 1 || getTimesClicked() <= 10){
        QLabel label;
        label.setText("49");
        timesClicked++;
    };
    counter++;
}
